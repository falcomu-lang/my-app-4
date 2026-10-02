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
            ObjectDetectionParameters = new List<ObjectDetectionParameterSettings>();
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

        public List<ObjectDetectionParameterSettings> ObjectDetectionParameters { get; private set; }
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
            SourceRelationType = string.Empty;
            SourceRelationId = string.Empty;
            SourceMaskMode = "Legacy";
            SourceMaskPrimaryType = string.Empty;
            SourceMaskPrimaryId = string.Empty;
            SourceMaskOperation = "None";
            SourceMaskSecondaryType = string.Empty;
            SourceMaskSecondaryId = string.Empty;
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
            EnableRotationAnalysis = false;
        }

        public string Id { get; set; }

        public string DisplayName { get; set; }

        // The source is stored independently from the display name and list order.
        public string SourceType { get; set; }

        public string SourceId { get; set; }

        // Used only when SourceId is not configured. A configured block source
        // always takes precedence over this fallback relation source.
        public string SourceRelationType { get; set; }

        public string SourceRelationId { get; set; }

        // New source-mask configuration. Legacy SourceType/SourceId and
        // SourceRelation* remain intact so existing profiles keep working.
        public string SourceMaskMode { get; set; }

        public string SourceMaskPrimaryType { get; set; }

        public string SourceMaskPrimaryId { get; set; }

        public string SourceMaskOperation { get; set; }

        public string SourceMaskSecondaryType { get; set; }

        public string SourceMaskSecondaryId { get; set; }

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

        // When enabled, object-definition results also keep per-object
        // rotation geometry calculated from the final retained MASK.
        public bool EnableRotationAnalysis { get; set; }

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

    public class ObjectDetectionDefectCoreSettings
    {
        public ObjectDetectionDefectCoreSettings()
        {
            Id = Guid.NewGuid().ToString("N");
            CoreKey = string.Empty;
            ContrastGain = 1.0;
            PreprocessMethod = "None";
            DefectEnhancementMethod = "None";
            LocalBackgroundKernelSize = 31;
            LocalBackgroundGain = 1.5;
            ClaheClipLimit = 2.0;
            ClaheTileGridSize = 8;
            GaussianKernelWidth = 3;
            GaussianKernelHeight = 3;
            GaussianSigmaX = 0;
            GaussianSigmaY = 0;
            MedianKernelSize = 3;
            DarkThresholdEnabled = false;
            DarkThreshold = 0;
            BrightThresholdEnabled = false;
            BrightThreshold = 255;
            ErodeEnabled = false;
            ErodeKernelSize = 3;
            ErodeIterations = 0;
            DilateEnabled = false;
            DilateKernelSize = 3;
            DilateIterations = 0;
            MinimumArea = 0;
            MinimumWidthMillimeters = 0;
            MaximumWidthMillimeters = 0;
            MinimumHeightMillimeters = 0;
            MaximumHeightMillimeters = 0;
            ShowMask = true;
            ShowRedBoxes = true;
            ShowOrangeBoxes = true;
            Enabled = true;
        }

        public string Id { get; set; }

        public string CoreKey { get; set; }

        public double ContrastGain { get; set; }

        public string PreprocessMethod { get; set; }

        public string DefectEnhancementMethod { get; set; }

        public int LocalBackgroundKernelSize { get; set; }

        public double LocalBackgroundGain { get; set; }

        public double ClaheClipLimit { get; set; }

        public int ClaheTileGridSize { get; set; }

        public int GaussianKernelWidth { get; set; }

        public int GaussianKernelHeight { get; set; }

        public double GaussianSigmaX { get; set; }

        public double GaussianSigmaY { get; set; }

        public int MedianKernelSize { get; set; }

        public bool DarkThresholdEnabled { get; set; }

        public int DarkThreshold { get; set; }

        public bool BrightThresholdEnabled { get; set; }

        public int BrightThreshold { get; set; }

        public bool ErodeEnabled { get; set; }

        public int ErodeKernelSize { get; set; }

        public int ErodeIterations { get; set; }

        public bool DilateEnabled { get; set; }

        public int DilateKernelSize { get; set; }

        public int DilateIterations { get; set; }

        public double MinimumArea { get; set; }

        public double MinimumWidthMillimeters { get; set; }

        public double MaximumWidthMillimeters { get; set; }

        public double MinimumHeightMillimeters { get; set; }

        public double MaximumHeightMillimeters { get; set; }

        public bool ShowMask { get; set; }

        public bool ShowRedBoxes { get; set; }

        public bool ShowOrangeBoxes { get; set; }

        public bool Enabled { get; set; }
    }

    public class ObjectDetectionParameterSettings
    {
        public ObjectDetectionParameterSettings()
        {
            Id = Guid.NewGuid().ToString("N");
            ObjectDefinitionId = string.Empty;
            ColumnCount = 0;
            RowCount = 0;
            CameraXMillimetersPerPixel = 1.0;
            CameraYMillimetersPerPixel = 1.0;
            CameraPrecisionConfigured = false;
            SourceMaskMode = "ObjectDefinition";
            SourceMaskPrimaryType = "ObjectDefinition";
            SourceMaskPrimaryId = string.Empty;
            SourceMaskPrimaryNamespace = string.Empty;
            SourceMaskOperation = "None";
            SourceMaskSecondaryType = string.Empty;
            SourceMaskSecondaryId = string.Empty;
            SourceMaskSecondaryNamespace = string.Empty;
            FlatFieldMaskMode = "Direct";
            FlatFieldMaskPrimaryType = string.Empty;
            FlatFieldMaskPrimaryId = string.Empty;
            FlatFieldMaskPrimaryNamespace = string.Empty;
            FlatFieldMaskOperation = "None";
            FlatFieldMaskSecondaryType = string.Empty;
            FlatFieldMaskSecondaryId = string.Empty;
            FlatFieldMaskSecondaryNamespace = string.Empty;
            FlatFieldMaskDisplayName = "未指定來源 MASK";
            FlatFieldUseMaskMode = "Direct";
            FlatFieldUseMaskOperation = "None";
            FlatFieldUseMaskDisplayName = "沿用平場校正來源 MASK";
            FlatFieldSamplePositionConfigured = false;
            FlatFieldSampleYRatio = 0.5;
            FlatFieldSamplingHeight = 100;
            FlatFieldTargetGray = 128;
            FlatFieldSmoothingMode = "MovingAverage";
            FlatFieldSmoothingWindow = 51;
            FlatFieldSavedProfileData = string.Empty;
            FlatFieldSavedSettingsSignature = string.Empty;
            MeasurementMode = "Single";
            MeasurementDirection = "Horizontal";
            MeasurementLineCount = 1;
            MeasurementLengthMode = "FirstContinuous";
            MeasurementClipLinesToMask = false;
            MeasurementLineConfigured = false;
            MeasurementName = "量測線1";
            MeasurementLineOrder = "LeftToRight";
            MeasurementSecondLineOrder = "LeftToRight";
            MeasurementSourceMaskDisplayName = "未指定來源 MASK";
            MeasurementRecords = new List<ObjectDetectionMeasurementRecordSettings>();
            GoodJudgementRules = new List<ObjectDetectionGoodJudgementRuleSettings>();
            DefectInspectionRegionId = Guid.NewGuid().ToString("N");
            DefectInspectionRegionObjectDefinitionId = string.Empty;
            DefectInspectionRegionReferenceObjectNumber = 0;
            DefectInspectionRegionConfigured = false;
            DefectInspectionRegionLeft = 0;
            DefectInspectionRegionTop = 0;
            DefectInspectionRegionRight = 1;
            DefectInspectionRegionBottom = 1;
            DefectParallelExecutionEnabled = false;
            DefectIntegrationMergeDark = false;
            DefectIntegrationMergeBright = false;
            DefectIntegrationMergeDarkBright = false;
            DefectIntegrationMergeDistancePixels = 0;
            DefectFrequencyEnabled = true;
            DefectFrequencyScanHeight = 50;
            DefectFrequencySensitivity = 3.0;
            DefectFrequencyShowHeatmap = true;
            DefectFrequencyShowAnomalyBoxes = true;
            DefectDetectionCores = new List<ObjectDetectionDefectCoreSettings>
            {
                new ObjectDetectionDefectCoreSettings { CoreKey = "FlatField" },
                new ObjectDetectionDefectCoreSettings { CoreKey = "Contrast1", ContrastGain = 1.25 },
                new ObjectDetectionDefectCoreSettings { CoreKey = "Contrast2", ContrastGain = 1.5 },
                new ObjectDetectionDefectCoreSettings { CoreKey = "Contrast3", ContrastGain = 2.0 }
            };
        }

        public string Id { get; set; }

        public string DisplayName { get; set; }

        public string Parameters { get; set; }

        public string ObjectDefinitionId { get; set; }

        public int ColumnCount { get; set; }

        public int RowCount { get; set; }

        public double CameraXMillimetersPerPixel { get; set; }

        public double CameraYMillimetersPerPixel { get; set; }

        public bool CameraPrecisionConfigured { get; set; }

        // One shared source-MASK definition is applied to every numbered
        // object in this detection parameter.
        public string SourceMaskMode { get; set; }

        public string SourceMaskPrimaryType { get; set; }

        public string SourceMaskPrimaryId { get; set; }

        public string SourceMaskPrimaryNamespace { get; set; }

        public string SourceMaskOperation { get; set; }

        public string SourceMaskSecondaryType { get; set; }

        public string SourceMaskSecondaryId { get; set; }

        public string SourceMaskSecondaryNamespace { get; set; }

        public string FlatFieldMaskMode { get; set; }

        public string FlatFieldMaskPrimaryType { get; set; }

        public string FlatFieldMaskPrimaryId { get; set; }

        public string FlatFieldMaskPrimaryNamespace { get; set; }

        public string FlatFieldMaskOperation { get; set; }

        public string FlatFieldMaskSecondaryType { get; set; }

        public string FlatFieldMaskSecondaryId { get; set; }

        public string FlatFieldMaskSecondaryNamespace { get; set; }

        public string FlatFieldMaskDisplayName { get; set; }

        public string FlatFieldUseMaskMode { get; set; }

        public string FlatFieldUseMaskPrimaryType { get; set; }

        public string FlatFieldUseMaskPrimaryId { get; set; }

        public string FlatFieldUseMaskPrimaryNamespace { get; set; }

        public string FlatFieldUseMaskOperation { get; set; }

        public string FlatFieldUseMaskSecondaryType { get; set; }

        public string FlatFieldUseMaskSecondaryId { get; set; }

        public string FlatFieldUseMaskSecondaryNamespace { get; set; }

        public string FlatFieldUseMaskDisplayName { get; set; }

        public bool FlatFieldSamplePositionConfigured { get; set; }

        public double FlatFieldSampleYRatio { get; set; }

        public int FlatFieldSamplingHeight { get; set; }

        public int FlatFieldTargetGray { get; set; }

        public string FlatFieldSmoothingMode { get; set; }

        public int FlatFieldSmoothingWindow { get; set; }

        public int FlatFieldSavedImageWidth { get; set; }

        public int FlatFieldSavedTargetGray { get; set; }

        public string FlatFieldSavedProfileData { get; set; }

        public string FlatFieldSavedSettingsSignature { get; set; }

        // Size-measurement geometry is stored as normalized coordinates inside
        // the selected object's ROI, so it can be reused for every object.
        public string MeasurementMode { get; set; }

        public string MeasurementDirection { get; set; }

        public int MeasurementLineCount { get; set; }

        public string MeasurementLengthMode { get; set; }

        public bool MeasurementClipLinesToMask { get; set; }

        public bool MeasurementLineConfigured { get; set; }

        public string MeasurementName { get; set; }

        public string MeasurementLineOrder { get; set; }

        public string MeasurementSecondLineOrder { get; set; }

        public bool MeasurementStartOutsideRoi { get; set; }

        public bool MeasurementEndOutsideRoi { get; set; }

        public bool MeasurementSecondStartOutsideRoi { get; set; }

        public bool MeasurementSecondEndOutsideRoi { get; set; }

        public string MeasurementSourceMaskDisplayName { get; set; }

        public double MeasurementStartX { get; set; }

        public double MeasurementStartY { get; set; }

        public double MeasurementEndX { get; set; }

        public double MeasurementEndY { get; set; }

        public double MeasurementSecondStartX { get; set; }

        public double MeasurementSecondStartY { get; set; }

        public double MeasurementSecondEndX { get; set; }

        public double MeasurementSecondEndY { get; set; }

        public List<ObjectDetectionMeasurementRecordSettings> MeasurementRecords { get; set; }

        public List<ObjectDetectionGoodJudgementRuleSettings> GoodJudgementRules { get; set; }

        public string DefectInspectionRegionId { get; set; }

        public string DefectInspectionRegionObjectDefinitionId { get; set; }

        public int DefectInspectionRegionReferenceObjectNumber { get; set; }

        public bool DefectInspectionRegionConfigured { get; set; }

        public double DefectInspectionRegionLeft { get; set; }

        public double DefectInspectionRegionTop { get; set; }

        public double DefectInspectionRegionRight { get; set; }

        public double DefectInspectionRegionBottom { get; set; }

        public bool DefectParallelExecutionEnabled { get; set; }

        public bool DefectIntegrationMergeDark { get; set; }

        public bool DefectIntegrationMergeBright { get; set; }

        public bool DefectIntegrationMergeDarkBright { get; set; }

        public double DefectIntegrationMergeDistancePixels { get; set; }

        public bool DefectFrequencyEnabled { get; set; }

        public int DefectFrequencyScanHeight { get; set; }

        public double DefectFrequencySensitivity { get; set; }

        public bool DefectFrequencyShowHeatmap { get; set; }

        public bool DefectFrequencyShowAnomalyBoxes { get; set; }

        public List<ObjectDetectionDefectCoreSettings> DefectDetectionCores { get; set; }
    }

    public class ObjectDetectionGoodJudgementRuleSettings
    {
        public ObjectDetectionGoodJudgementRuleSettings()
        {
            Id = Guid.NewGuid().ToString("N");
            Number = 0;
            Name = "判定條件";
            Enabled = true;
            CalculationExpression = string.Empty;
            SpecificationExpression = string.Empty;
            AlternativeCalculationExpression = string.Empty;
            AlternativeSpecificationExpression = string.Empty;
        }

        public string Id { get; set; }

        public int Number { get; set; }

        public string Name { get; set; }

        public bool Enabled { get; set; }

        public string CalculationExpression { get; set; }

        public string SpecificationExpression { get; set; }

        public string AlternativeCalculationExpression { get; set; }

        public string AlternativeSpecificationExpression { get; set; }
    }

    public class ObjectDetectionMeasurementRecordSettings
    {
        public ObjectDetectionMeasurementRecordSettings()
        {
            Id = Guid.NewGuid().ToString("N");
            Number = 0;
            Name = "量測線";
            Mode = "Single";
            Direction = "Horizontal";
            LineCount = 1;
            LengthMode = "FirstContinuous";
            LineOrder = "LeftToRight";
            SecondLineOrder = "LeftToRight";
            SourceMaskMode = "Direct";
            SourceMaskOperation = "None";
        }

        public string Id { get; set; }

        public int Number { get; set; }

        public string Name { get; set; }

        public string Mode { get; set; }

        public string Direction { get; set; }

        public int LineCount { get; set; }

        public string LengthMode { get; set; }

        public string LineOrder { get; set; }

        public string SecondLineOrder { get; set; }

        public bool StartOutsideRoi { get; set; }

        public bool EndOutsideRoi { get; set; }

        public bool SecondStartOutsideRoi { get; set; }

        public bool SecondEndOutsideRoi { get; set; }

        public string SourceMaskDisplayName { get; set; }

        public string SourceMaskMode { get; set; }

        public string SourceMaskPrimaryType { get; set; }

        public string SourceMaskPrimaryId { get; set; }

        public string SourceMaskPrimaryNamespace { get; set; }

        public string SourceMaskOperation { get; set; }

        public string SourceMaskSecondaryType { get; set; }

        public string SourceMaskSecondaryId { get; set; }

        public string SourceMaskSecondaryNamespace { get; set; }

        public double StartX { get; set; }

        public double StartY { get; set; }

        public double EndX { get; set; }

        public double EndY { get; set; }

        public double SecondStartX { get; set; }

        public double SecondStartY { get; set; }

        public double SecondEndX { get; set; }

        public double SecondEndY { get; set; }
    }
}
