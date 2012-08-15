﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;
using SirenOfShame.Lib.Settings;
using SirenOfShame.Lib.Watcher;

namespace SirenOfShame
{
    public partial class ViewBuilds : UserControl
    {
        private SirenOfShameSettings _settings;
        private readonly Timer _prettyDateTimer = new Timer();
        private List<BuildStatusDto> _lastBuildStatusDtos = new List<BuildStatusDto>();

        private class BuildStatusDtoAndControl
        {
            public BuildStatusDto BuildStatusDto { get; set; }
            public ViewBuildBase Control { get; set; }
        }

        public ViewBuilds()
        {
            InitializeComponent();

            _prettyDateTimer.Interval = 20000;
            _prettyDateTimer.Tick += PrettyDateTimerOnTick;
            _prettyDateTimer.Start();
        }

        private void InitializeForBuild(string buildId)
        {
            bool viewAllBuilds = buildId == null;
            var buildStatusDto = _lastBuildStatusDtos.FirstOrDefault(i => i.Id == buildId);
            InitializeLabelsForBuild(viewAllBuilds);
            InitializeViewBuildBig(buildStatusDto);
            InitializeDisplayModes();
            InitializeSmallBuildsVisibility(buildId);
        }

        private void InitializeSmallBuildsVisibility(string activeBuildId)
        {
            var smallBuilds = GetSmallViewBuilds();
            foreach (var viewBuildSmall in smallBuilds)
            {
                var isActive = viewBuildSmall.BuildId == activeBuildId;
                viewBuildSmall.Visible = !isActive;
            }
        }

        private void InitializeViewBuildBig(BuildStatusDto buildStatusDto)
        {
            if (buildStatusDto == null) return;
            _viewBuildBig.InitializeForBuild(buildStatusDto);
        }

        private void InitializeLabelsForBuild(bool viewAllBuilds)
        {
            _back.Visible = !viewAllBuilds;
            _viewBuildBig.Visible = !viewAllBuilds;
        }

        private void PrettyDateTimerOnTick(object sender, EventArgs eventArgs)
        {
            GetViewBuilds().ToList().ForEach(i => i.RecalculatePrettyDate());
        }

        public void RefreshBuildStatuses(RefreshStatusEventArgs args)
        {
            _lastBuildStatusDtos = args.BuildStatusDtos.ToList();
            var buildStatusDtosAndControl = GetBuildStatusDtoAndControls(_lastBuildStatusDtos).ToList();
            RemoveAllChildControlsIfBuildCountOrBuildNamesChanged(buildStatusDtosAndControl, _lastBuildStatusDtos);
            if (NoChildControlsExist)
            {
                CreateControlsAndAddToPanels(_lastBuildStatusDtos);
            }
            else
            {
                UpdateExistingControls(buildStatusDtosAndControl);
            }
            InitializeDisplayModes();
        }

        private bool IsViewBuildBigVisible
        {
            get { return _viewBuildBig.Visible; }
        }
        
        private void InitializeDisplayModes()
        {
            int idealSmallControlCountPerRow = GetIdealSmallControlCountPerRow();
            _viewBuildBig.Width = GetIdealBigControlWidth(idealSmallControlCountPerRow);
            var idealSmallControlCount = idealSmallControlCountPerRow*2;
            var controls = GetSmallViewBuilds().ToList();
            for (int i = 0; i < controls.Count; i++)
            {
                var control = controls[i];
                var mode = GetDisplayMode(idealSmallControlCount, i);
                control.SetDisplayMode(mode);
            }
        }

        private static int GetIdealBigControlWidth(int idealSmallControlCountPerRow)
        {
            idealSmallControlCountPerRow = Math.Max(idealSmallControlCountPerRow, 2);
            int marginWidths = ((idealSmallControlCountPerRow - 1)*ViewBuildSmall.MARGIN*2);
            int smallControlWidths = (idealSmallControlCountPerRow*ViewBuildSmall.WIDTH);
            return smallControlWidths + marginWidths;
        }

        private ViewBuildDisplayMode GetDisplayMode(int controlCount, int idealSmallControlCount)
        {
            if (IsViewBuildBigVisible) return ViewBuildDisplayMode.Tiny;
            return idealSmallControlCount < controlCount ? ViewBuildDisplayMode.Normal : ViewBuildDisplayMode.Tiny;
        }

        private void RemoveAllChildControlsIfBuildCountOrBuildNamesChanged(ICollection buildStatusDtosAndControl, ICollection<BuildStatusDto> buildStatusDtos)
        {
            int visibleViewBuildControls = VisibleViewBuildControls;
            bool numberOfBuildsChanged = visibleViewBuildControls != 0 && visibleViewBuildControls != buildStatusDtos.Count();
            var anyBuildNameChanged = buildStatusDtosAndControl.Count != buildStatusDtos.Count;
            if (numberOfBuildsChanged || anyBuildNameChanged)
            {
                RemoveAllSmallBuildControls();
            }
        }

        private int VisibleViewBuildControls
        {
            get { return _mainFlowLayoutPanel.Controls.Cast<Control>().Count(i => i.Visible); }
        }

        private bool NoChildControlsExist
        {
            get { return VisibleViewBuildControls == 0; }
        }

        private void RemoveAllSmallBuildControls()
        {
            var smallBuilds = GetSmallViewBuilds().ToList();
            foreach (var viewBuildSmall in smallBuilds)
            {
                _mainFlowLayoutPanel.Controls.Remove(viewBuildSmall);
                viewBuildSmall.Dispose();
            }
            _viewBuildBig.Visible = false;
        }

        private IEnumerable<BuildStatusDtoAndControl> GetBuildStatusDtoAndControls(IEnumerable<BuildStatusDto> buildStatusDtos)
        {
            return from control in GetViewBuilds() 
                   where control.Visible
                   join buildStatusDto in buildStatusDtos on control.BuildId equals buildStatusDto.Id
                   orderby buildStatusDto.LocalStartTime descending 
                   select new BuildStatusDtoAndControl { Control = control, BuildStatusDto = buildStatusDto };
        }

        private void UpdateExistingControls(List<BuildStatusDtoAndControl> buildStatusDtosAndControl)
        {
            UpdateDetailsInExistingControls(buildStatusDtosAndControl);
            SortExistingControls(buildStatusDtosAndControl);
        }

        private static void UpdateDetailsInExistingControls(List<BuildStatusDtoAndControl> buildStatusDtosAndControl)
        {
            foreach (var buildStatusAndControl in buildStatusDtosAndControl)
            {
                buildStatusAndControl.Control.UpdateListItem(buildStatusAndControl.BuildStatusDto);
            }
        }

        private void SortExistingControls(List<BuildStatusDtoAndControl> buildStatusDtosAndControl)
        {
            var buildsExceptActiveOne = buildStatusDtosAndControl.Where(i => i.Control != _viewBuildBig).ToList();
            var startIndex = _viewBuildBig.Visible ? 1 : 0;
            for (int i = 0; i < buildsExceptActiveOne.Count; i++)
            {
                var buildStatusAndControl = buildsExceptActiveOne[i];
                _mainFlowLayoutPanel.Controls.SetChildIndex(buildStatusAndControl.Control, i + startIndex);
            }
        }

        private void CreateControlsAndAddToPanels(IEnumerable<BuildStatusDto> buildStatusDtos)
        {
            var buildsOrdered = buildStatusDtos
                .OrderByDescending(i => i.LocalStartTime)
                .ToList();

            buildsOrdered
                .Select(CreateViewBuildSmall)
                .ToList()
                .ForEach(i => _mainFlowLayoutPanel.Controls.Add(i));
        }

        private ViewBuildSmall CreateViewBuildSmall(BuildStatusDto i)
        {
            var viewBuildSmall = new ViewBuildSmall(i, _settings);
            viewBuildSmall.Click += ViewBuildSmallOnClick;
            return viewBuildSmall;
        }

        private void ViewBuildSmallOnClick(object sender, EventArgs eventArgs)
        {
            ViewBuildSmall viewBuildSmall = (ViewBuildSmall)sender;
            InitializeForBuild(viewBuildSmall.BuildId);
        }

        private int GetIdealSmallControlCountPerRow()
        {
            return (_mainFlowLayoutPanel.Width) / (ViewBuildSmall.MARGIN + ViewBuildSmall.WIDTH + ViewBuildSmall.MARGIN);
        }

        private void ViewBuildsResize(object sender, EventArgs e)
        {
            InitializeDisplayModes();
        }
        
        private IEnumerable<ViewBuildSmall> GetSmallViewBuilds()
        {
            return _mainFlowLayoutPanel.Controls
                .Cast<Control>()
                .Select(i => i as ViewBuildSmall)
                .Where(i => i != null);
        }
        
        private IEnumerable<ViewBuildBase> GetViewBuilds()
        {
            return _mainFlowLayoutPanel.Controls.Cast<ViewBuildBase>();
        }

        public void Initialize(SirenOfShameSettings settings)
        {
            _settings = settings;
            _viewBuildBig.Settings = _settings;
        }

        private void BackClick(object sender, EventArgs e)
        {
            InitializeForBuild(null);
        }
    }
}
