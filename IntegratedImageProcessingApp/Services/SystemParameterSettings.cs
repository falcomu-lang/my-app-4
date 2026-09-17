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
        public Rectangle Bounds { get; set; }
    }

    public class ImageProcessingStepSettings
    {
        public string Id { get; set; }

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

    public class ImageRelationSettings
    {
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
        public string Id { get; set; }

        public string ParentGroupId { get; set; }

        public string DisplayName { get; set; }
    }

    public class ObjectJudgementSettings
    {
        public ObjectJudgementSettings()
        {
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
        public string Id { get; set; }

        public string DisplayName { get; set; }

        public string Method { get; set; }

        public string Parameters { get; set; }
    }

    public class ObjectJudgementGroupSettings
    {
        public string Id { get; set; }

        public string ParentGroupId { get; set; }

        public string DisplayName { get; set; }
    }

    public class ObjectDefinitionSettings
    {
        public ObjectDefinitionSettings()
        {
            ObjectJudgementIds = new List<string>();
        }

        public string Id { get; set; }

        public string DisplayName { get; set; }

        public List<string> ObjectJudgementIds { get; private set; }
    }
}
