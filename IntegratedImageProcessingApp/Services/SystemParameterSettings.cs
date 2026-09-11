using System.Drawing;
using System.Collections.Generic;

namespace IntegratedImageProcessingApp.Services
{
    public class SystemParameterSettings
    {
        public SystemParameterSettings()
        {
            RoiRegions = new List<RoiRegionSettings>();
            ImageProcessingSteps = new List<ImageProcessingStepSettings>();
            ImageProcessingGroups = new List<ImageProcessingGroupSettings>();
        }

        public string LastImagePath { get; set; }

        public bool RoiEnabled { get; set; }

        public Rectangle Roi { get; set; }

        public List<RoiRegionSettings> RoiRegions { get; private set; }

        public List<ImageProcessingStepSettings> ImageProcessingSteps { get; private set; }

        public List<ImageProcessingGroupSettings> ImageProcessingGroups { get; private set; }
    }

    public class RoiRegionSettings
    {
        public Rectangle Bounds { get; set; }
    }

    public class ImageProcessingStepSettings
    {
        public string DisplayName { get; set; }

        public string GroupId { get; set; }

        public string Method { get; set; }

        public string Parameters { get; set; }
    }

    public class ImageProcessingGroupSettings
    {
        public string Id { get; set; }

        public string ParentGroupId { get; set; }

        public string DisplayName { get; set; }
    }
}
