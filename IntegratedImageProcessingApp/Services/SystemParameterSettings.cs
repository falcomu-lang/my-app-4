using System;
using System.Drawing;
using System.Collections.Generic;

namespace IntegratedImageProcessingApp.Services
{
    public class SystemParameterSettings
    {
        public SystemParameterSettings()
        {
            RoiRegions = new List<RoiRegionSettings>();
            ImagePreprocessingSteps = new List<ImageProcessingStepSettings>();
            ImagePreprocessingGroups = new List<ImageProcessingGroupSettings>();
            ImageProcessingSteps = new List<ImageProcessingStepSettings>();
            ImageProcessingGroups = new List<ImageProcessingGroupSettings>();
            ImageRelations = new List<ImageRelationSettings>();
            ImageRelationGroups = new List<ImageRelationGroupSettings>();
            ObjectJudgements = new List<ObjectJudgementSettings>();
            ObjectJudgementGroups = new List<ObjectJudgementGroupSettings>();
            ObjectDefinitions = new List<ObjectDefinitionSettings>();
        }

        public string LastImagePath { get; set; }

        public bool RoiEnabled { get; set; }

        public Rectangle Roi { get; set; }

        public List<RoiRegionSettings> RoiRegions { get; private set; }

        public List<ImageProcessingStepSettings> ImagePreprocessingSteps { get; private set; }

        public List<ImageProcessingGroupSettings> ImagePreprocessingGroups { get; private set; }

        public List<ImageProcessingStepSettings> ImageProcessingSteps { get; private set; }

        public List<ImageProcessingGroupSettings> ImageProcessingGroups { get; private set; }

        public List<ImageRelationSettings> ImageRelations { get; private set; }

        public List<ImageRelationGroupSettings> ImageRelationGroups { get; private set; }

        public List<ObjectJudgementSettings> ObjectJudgements { get; private set; }

        public List<ObjectJudgementGroupSettings> ObjectJudgementGroups { get; private set; }

        public List<ObjectDefinitionSettings> ObjectDefinitions { get; private set; }
    }

    public class RoiRegionSettings
    {
        public RoiRegionSettings()
        {
            Id = Guid.NewGuid().ToString("N");
        }

        public string Id { get; set; }

        public Rectangle Bounds { get; set; }
    }

    public class ImageProcessingStepSettings
    {
        public ImageProcessingStepSettings()
        {
            Id = Guid.NewGuid().ToString("N");
        }

        public string Id { get; set; }

        public string DisplayName { get; set; }

        public string GroupId { get; set; }

        public string Method { get; set; }

        public string Parameters { get; set; }
    }

    public class ImageProcessingGroupSettings
    {
        public ImageProcessingGroupSettings()
        {
            Id = Guid.NewGuid().ToString("N");
        }

        public string Id { get; set; }

        public string ParentGroupId { get; set; }

        public string DisplayName { get; set; }
    }

    public class ImageRelationSettings
    {
        public ImageRelationSettings()
        {
            Id = Guid.NewGuid().ToString("N");
        }

        public string Id { get; set; }

        public string DisplayName { get; set; }

        public string SourceType { get; set; }

        public string SourceId { get; set; }

        public string ProcessingType { get; set; }

        public string ProcessingId { get; set; }

        public string GroupId { get; set; }
    }

    public class ImageRelationGroupSettings
    {
        public ImageRelationGroupSettings()
        {
            Id = Guid.NewGuid().ToString("N");
        }

        public string Id { get; set; }

        public string ParentGroupId { get; set; }

        public string DisplayName { get; set; }
    }

    public class ObjectJudgementSettings
    {
        public ObjectJudgementSettings()
        {
            Id = Guid.NewGuid().ToString("N");
            ProcessingSteps = new List<ObjectJudgementProcessingSettings>();
        }

        public string Id { get; set; }

        public string DisplayName { get; set; }

        public string RelationType { get; set; }

        public string RelationId { get; set; }

        public string GroupId { get; set; }

        public List<ObjectJudgementProcessingSettings> ProcessingSteps { get; private set; }
    }

    public class ObjectJudgementProcessingSettings
    {
        public ObjectJudgementProcessingSettings()
        {
            Id = Guid.NewGuid().ToString("N");
        }

        public string Id { get; set; }

        public string DisplayName { get; set; }

        public string Method { get; set; }

        public string Parameters { get; set; }
    }

    public class ObjectJudgementGroupSettings
    {
        public ObjectJudgementGroupSettings()
        {
            Id = Guid.NewGuid().ToString("N");
        }

        public string Id { get; set; }

        public string ParentGroupId { get; set; }

        public string DisplayName { get; set; }
    }

    public class ObjectDefinitionSettings
    {
        public ObjectDefinitionSettings()
        {
            Id = Guid.NewGuid().ToString("N");
            ObjectJudgementIds = new List<string>();
            ProcessingSteps = new List<ObjectDefinitionProcessingSettings>();
            SourceType = "ObjectJudgement";
            Connectivity = 8;
            MinArea = 0;
            MaxArea = 0;
            NumberingOrder = "TopToBottomLeftToRight";
            MergeMethod = "None";
            MaxMergeDistance = 0;
            GroupMinArea = 0;
            GroupMaxArea = 0;
            ResultBoxLineWidth = 2;
            ResultNumberFontSize = 10;
        }

        public string Id { get; set; }

        public string DisplayName { get; set; }

        // The source is stored independently from the display name and list order.
        public string SourceType { get; set; }

        public string SourceId { get; set; }

        public int Connectivity { get; set; }

        public double MinArea { get; set; }

        public double MaxArea { get; set; }

        public string NumberingOrder { get; set; }

        public string MergeMethod { get; set; }

        public int MaxMergeDistance { get; set; }

        public double GroupMinArea { get; set; }

        public double GroupMaxArea { get; set; }

        public int ResultBoxLineWidth { get; set; }

        public int ResultNumberFontSize { get; set; }

        public List<string> ObjectJudgementIds { get; private set; }

        public List<ObjectDefinitionProcessingSettings> ProcessingSteps { get; private set; }
    }

    public class ObjectDefinitionProcessingSettings
    {
        public ObjectDefinitionProcessingSettings()
        {
            Id = Guid.NewGuid().ToString("N");
        }

        public string Id { get; set; }

        public string DisplayName { get; set; }

        public string Method { get; set; }

        public string Parameters { get; set; }
    }
}
