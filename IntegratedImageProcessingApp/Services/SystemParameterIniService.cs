using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.IO;

namespace IntegratedImageProcessingApp.Services
{
    public class SystemParameterIniService
    {
        private const string SectionSystem = "System";
        private const string SectionRoi = "ROI";
        private const string SectionRois = "ROIs";
        private const string SectionImageProcessing = "ImageProcessing";
        private const string SectionImagePreprocessing = "ImagePreprocessing";
        private const string SectionImageRelations = "ImageRelations";
        private const string SectionObjectJudgement = "ObjectJudgement";
        private const string SectionObjectJudgementGroups = "ObjectJudgementGroups";
        private const string SectionObjectDefinition = "ObjectDefinition";
        private readonly string filePath;

        public SystemParameterIniService(string filePath)
        {
            this.filePath = filePath;
        }

        public SystemParameterSettings Load()
        {
            var settings = new SystemParameterSettings();
            Dictionary<string, Dictionary<string, string>> sections = ReadSections();

            settings.LastImagePath = GetValue(sections, SectionSystem, "LastImagePath", string.Empty);
            settings.RoiEnabled = GetBool(sections, SectionRoi, "Enabled", false);
            settings.Roi = new Rectangle(
                GetInt(sections, SectionRoi, "X", 0),
                GetInt(sections, SectionRoi, "Y", 0),
                GetInt(sections, SectionRoi, "Width", 0),
                GetInt(sections, SectionRoi, "Height", 0));

            if (settings.Roi.Width <= 0 || settings.Roi.Height <= 0)
            {
                settings.RoiEnabled = false;
            }

            int roiCount = GetInt(sections, SectionRois, "Count", 0);
            for (int index = 1; index <= roiCount; index++)
            {
                var roi = new Rectangle(
                    GetInt(sections, SectionRois, "Roi" + index + ".X", 0),
                    GetInt(sections, SectionRois, "Roi" + index + ".Y", 0),
                    GetInt(sections, SectionRois, "Roi" + index + ".Width", 0),
                    GetInt(sections, SectionRois, "Roi" + index + ".Height", 0));
                if (roi.Width > 0 && roi.Height > 0)
                {
                    settings.RoiRegions.Add(new RoiRegionSettings
                    {
                        Id = GetValue(sections, SectionRois, "Roi" + index + ".Id", string.Empty),
                        Bounds = roi
                    });
                }
            }

            if (settings.RoiRegions.Count == 0 && settings.RoiEnabled)
            {
                settings.RoiRegions.Add(new RoiRegionSettings { Bounds = settings.Roi });
            }
            else if (settings.RoiRegions.Count > 0)
            {
                settings.RoiEnabled = true;
                settings.Roi = settings.RoiRegions[0].Bounds;
            }

            int imageProcessingStepCount = GetInt(sections, SectionImageProcessing, "Count", 0);
            for (int index = 1; index <= imageProcessingStepCount; index++)
            {
                settings.ImageProcessingSteps.Add(new ImageProcessingStepSettings
                {
                    Id = GetValue(sections, SectionImageProcessing, "Step" + index + ".Id", string.Empty),
                    DisplayName = GetValue(sections, SectionImageProcessing, "Step" + index + ".DisplayName", string.Empty),
                    GroupId = GetValue(sections, SectionImageProcessing, "Step" + index + ".GroupId", string.Empty),
                    Method = GetValue(sections, SectionImageProcessing, "Step" + index + ".Method", string.Empty),
                    Parameters = GetValue(sections, SectionImageProcessing, "Step" + index + ".Parameters", string.Empty)
                });
            }

            int imagePreprocessingStepCount = GetInt(sections, SectionImagePreprocessing, "Count", 0);
            for (int index = 1; index <= imagePreprocessingStepCount; index++)
            {
                settings.ImagePreprocessingSteps.Add(new ImageProcessingStepSettings
                {
                    Id = GetValue(sections, SectionImagePreprocessing, "Step" + index + ".Id", string.Empty),
                    DisplayName = GetValue(sections, SectionImagePreprocessing, "Step" + index + ".DisplayName", string.Empty),
                    GroupId = GetValue(sections, SectionImagePreprocessing, "Step" + index + ".GroupId", string.Empty),
                    Method = GetValue(sections, SectionImagePreprocessing, "Step" + index + ".Method", string.Empty),
                    Parameters = GetValue(sections, SectionImagePreprocessing, "Step" + index + ".Parameters", string.Empty)
                });
            }

            EnsureStepIds(settings.ImageProcessingSteps);
            EnsureStepIds(settings.ImagePreprocessingSteps);

            int relationGroupCount = GetInt(sections, SectionImageRelations, "GroupCount", 0);
            for (int index = 1; index <= relationGroupCount; index++)
            {
                string prefix = "Group" + index;
                string id = GetValue(sections, SectionImageRelations, prefix + ".Id", string.Empty);
                settings.ImageRelationGroups.Add(new ImageRelationGroupSettings
                {
                    Id = id,
                    ParentGroupId = GetValue(sections, SectionImageRelations, prefix + ".ParentGroupId", string.Empty),
                    DisplayName = GetValue(sections, SectionImageRelations, prefix + ".DisplayName", string.Empty)
                });
            }

            int relationCount = GetInt(sections, SectionImageRelations, "Count", 0);
            for (int index = 1; index <= relationCount; index++)
            {
                string prefix = "Relation" + index;
                string id = GetValue(sections, SectionImageRelations, prefix + ".Id", string.Empty);
                settings.ImageRelations.Add(new ImageRelationSettings
                {
                    Id = id,
                    DisplayName = GetValue(sections, SectionImageRelations, prefix + ".DisplayName", string.Empty),
                    SourceType = GetValue(sections, SectionImageRelations, prefix + ".SourceType", string.Empty),
                    SourceId = GetValue(sections, SectionImageRelations, prefix + ".SourceId", string.Empty),
                    ProcessingType = GetValue(sections, SectionImageRelations, prefix + ".ProcessingType", string.Empty),
                    ProcessingId = GetValue(sections, SectionImageRelations, prefix + ".ProcessingId", string.Empty),
                    GroupId = GetValue(sections, SectionImageRelations, prefix + ".GroupId", string.Empty)
                });
            }

            int objectJudgementGroupCount = GetInt(sections, SectionObjectJudgementGroups, "Count", 0);
            for (int index = 1; index <= objectJudgementGroupCount; index++)
            {
                string prefix = "Group" + index;
                string id = GetValue(sections, SectionObjectJudgementGroups, prefix + ".Id", string.Empty);
                settings.ObjectJudgementGroups.Add(new ObjectJudgementGroupSettings
                {
                    Id = id,
                    ParentGroupId = GetValue(
                        sections,
                        SectionObjectJudgementGroups,
                        prefix + ".ParentGroupId",
                        string.Empty),
                    DisplayName = GetValue(
                        sections,
                        SectionObjectJudgementGroups,
                        prefix + ".DisplayName",
                        string.Empty)
                });
            }

            int objectJudgementCount = GetInt(sections, SectionObjectJudgement, "Count", 0);
            for (int index = 1; index <= objectJudgementCount; index++)
            {
                string prefix = "Object" + index;
                string id = GetValue(sections, SectionObjectJudgement, prefix + ".Id", string.Empty);
                settings.ObjectJudgements.Add(new ObjectJudgementSettings
                {
                    Id = id,
                    DisplayName = GetValue(sections, SectionObjectJudgement, prefix + ".DisplayName", string.Empty),
                    RelationType = GetValue(sections, SectionObjectJudgement, prefix + ".RelationType", string.Empty),
                    RelationId = GetValue(sections, SectionObjectJudgement, prefix + ".RelationId", string.Empty),
                    GroupId = GetValue(sections, SectionObjectJudgement, prefix + ".GroupId", string.Empty)
                });

                ObjectJudgementSettings objectJudgement = settings.ObjectJudgements[settings.ObjectJudgements.Count - 1];
                int processingCount = GetInt(sections, SectionObjectJudgement, prefix + ".ProcessingCount", 0);
                for (int processingIndex = 1; processingIndex <= processingCount; processingIndex++)
                {
                    string processingPrefix = prefix + ".Processing" + processingIndex;
                    objectJudgement.ProcessingSteps.Add(new ObjectJudgementProcessingSettings
                    {
                        Id = GetValue(sections, SectionObjectJudgement, processingPrefix + ".Id", string.Empty),
                        DisplayName = GetValue(sections, SectionObjectJudgement, processingPrefix + ".DisplayName", string.Empty),
                        Method = GetValue(sections, SectionObjectJudgement, processingPrefix + ".Method", string.Empty),
                        Parameters = GetValue(sections, SectionObjectJudgement, processingPrefix + ".Parameters", string.Empty)
                    });
                }
            }

            int objectDefinitionCount = GetInt(sections, SectionObjectDefinition, "Count", 0);
            for (int index = 1; index <= objectDefinitionCount; index++)
            {
                string prefix = "Definition" + index;
                string id = GetValue(sections, SectionObjectDefinition, prefix + ".Id", string.Empty);
                var definition = new ObjectDefinitionSettings
                {
                    Id = id,
                    DisplayName = GetValue(
                        sections,
                        SectionObjectDefinition,
                        prefix + ".DisplayName",
                        string.Empty),
                    SourceType = GetValue(
                        sections,
                        SectionObjectDefinition,
                        prefix + ".SourceType",
                        "ObjectJudgement"),
                    SourceId = GetValue(
                        sections,
                        SectionObjectDefinition,
                        prefix + ".SourceId",
                        string.Empty),
                    Connectivity = GetInt(
                        sections,
                        SectionObjectDefinition,
                        prefix + ".Connectivity",
                        8),
                    MinArea = GetDouble(
                        sections,
                        SectionObjectDefinition,
                        prefix + ".MinArea",
                        0),
                    MaxArea = GetDouble(
                        sections,
                        SectionObjectDefinition,
                        prefix + ".MaxArea",
                        0),
                    NumberingOrder = GetValue(
                        sections,
                        SectionObjectDefinition,
                        prefix + ".NumberingOrder",
                        "TopToBottomLeftToRight"),
                    MergeMethod = GetValue(
                        sections,
                        SectionObjectDefinition,
                        prefix + ".MergeMethod",
                        "None"),
                    MaxMergeDistance = GetInt(
                        sections,
                        SectionObjectDefinition,
                        prefix + ".MaxMergeDistance",
                        0),
                    ResultBoxLineWidth = GetInt(
                        sections,
                        SectionObjectDefinition,
                        prefix + ".ResultBoxLineWidth",
                        2),
                    ResultNumberFontSize = GetInt(
                        sections,
                        SectionObjectDefinition,
                        prefix + ".ResultNumberFontSize",
                        10)
                };
                string memberIds = GetValue(
                    sections,
                    SectionObjectDefinition,
                    prefix + ".ObjectJudgementIds",
                    string.Empty);
                foreach (string memberId in memberIds.Split(new[] { '|' }, StringSplitOptions.RemoveEmptyEntries))
                {
                    definition.ObjectJudgementIds.Add(memberId);
                }

                int processingCount = GetInt(
                    sections,
                    SectionObjectDefinition,
                    prefix + ".ProcessingCount",
                    0);
                for (int processingIndex = 1; processingIndex <= processingCount; processingIndex++)
                {
                    string processingPrefix = prefix + ".Processing" + processingIndex;
                    definition.ProcessingSteps.Add(new ObjectDefinitionProcessingSettings
                    {
                        Id = GetValue(
                            sections,
                            SectionObjectDefinition,
                            processingPrefix + ".Id",
                            string.Empty),
                        DisplayName = GetValue(
                            sections,
                            SectionObjectDefinition,
                            processingPrefix + ".DisplayName",
                            string.Empty),
                        Method = GetValue(
                            sections,
                            SectionObjectDefinition,
                            processingPrefix + ".Method",
                            string.Empty),
                        Parameters = GetValue(
                            sections,
                            SectionObjectDefinition,
                            processingPrefix + ".Parameters",
                            string.Empty)
                    });
                }

                settings.ObjectDefinitions.Add(definition);
            }

            int imagePreprocessingGroupCount = GetInt(sections, SectionImagePreprocessing, "GroupCount", 0);
            for (int index = 1; index <= imagePreprocessingGroupCount; index++)
            {
                string keyPrefix = "Group" + index;
                string id = GetValue(sections, SectionImagePreprocessing, keyPrefix + ".Id", string.Empty);
                settings.ImagePreprocessingGroups.Add(new ImageProcessingGroupSettings
                {
                    Id = id,
                    ParentGroupId = GetValue(sections, SectionImagePreprocessing, keyPrefix + ".ParentGroupId", string.Empty),
                    DisplayName = GetValue(sections, SectionImagePreprocessing, keyPrefix + ".DisplayName", string.Empty)
                });
            }

            int imageProcessingGroupCount = GetInt(sections, SectionImageProcessing, "GroupCount", 0);
            for (int index = 1; index <= imageProcessingGroupCount; index++)
            {
                string keyPrefix = "Group" + index;
                string id = GetValue(sections, SectionImageProcessing, keyPrefix + ".Id", string.Empty);
                settings.ImageProcessingGroups.Add(new ImageProcessingGroupSettings
                {
                    Id = id,
                    ParentGroupId = GetValue(sections, SectionImageProcessing, keyPrefix + ".ParentGroupId", string.Empty),
                    DisplayName = GetValue(sections, SectionImageProcessing, keyPrefix + ".DisplayName", string.Empty)
                });
            }

            EnsureAllEntityIds(settings);
            return settings;
        }

        public void Save(SystemParameterSettings settings)
        {
            string directory = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            using (var writer = new StreamWriter(filePath, false))
            {
                writer.WriteLine("[System]");
                writer.WriteLine("LastImagePath={0}", Escape(settings.LastImagePath));
                writer.WriteLine();
                writer.WriteLine("[ROI]");
                writer.WriteLine("Enabled={0}", settings.RoiEnabled ? "true" : "false");
                writer.WriteLine("X={0}", settings.Roi.X.ToString(CultureInfo.InvariantCulture));
                writer.WriteLine("Y={0}", settings.Roi.Y.ToString(CultureInfo.InvariantCulture));
                writer.WriteLine("Width={0}", settings.Roi.Width.ToString(CultureInfo.InvariantCulture));
                writer.WriteLine("Height={0}", settings.Roi.Height.ToString(CultureInfo.InvariantCulture));
                writer.WriteLine();
                writer.WriteLine("[ROIs]");
                writer.WriteLine("Count={0}", settings.RoiRegions.Count.ToString(CultureInfo.InvariantCulture));
                for (int index = 0; index < settings.RoiRegions.Count; index++)
                {
                    Rectangle roi = settings.RoiRegions[index].Bounds;
                    string keyPrefix = "Roi" + (index + 1).ToString(CultureInfo.InvariantCulture);
                    writer.WriteLine("{0}.Id={1}", keyPrefix, Escape(settings.RoiRegions[index].Id));
                    writer.WriteLine("{0}.X={1}", keyPrefix, roi.X.ToString(CultureInfo.InvariantCulture));
                    writer.WriteLine("{0}.Y={1}", keyPrefix, roi.Y.ToString(CultureInfo.InvariantCulture));
                    writer.WriteLine("{0}.Width={1}", keyPrefix, roi.Width.ToString(CultureInfo.InvariantCulture));
                    writer.WriteLine("{0}.Height={1}", keyPrefix, roi.Height.ToString(CultureInfo.InvariantCulture));
                }

                writer.WriteLine();
                writer.WriteLine("[ImageProcessing]");
                writer.WriteLine("Count={0}", settings.ImageProcessingSteps.Count.ToString(CultureInfo.InvariantCulture));
                writer.WriteLine("GroupCount={0}", settings.ImageProcessingGroups.Count.ToString(CultureInfo.InvariantCulture));
                for (int index = 0; index < settings.ImageProcessingGroups.Count; index++)
                {
                    ImageProcessingGroupSettings group = settings.ImageProcessingGroups[index];
                    string keyPrefix = "Group" + (index + 1).ToString(CultureInfo.InvariantCulture);
                    writer.WriteLine("{0}.Id={1}", keyPrefix, Escape(group.Id));
                    writer.WriteLine("{0}.ParentGroupId={1}", keyPrefix, Escape(group.ParentGroupId));
                    writer.WriteLine("{0}.DisplayName={1}", keyPrefix, Escape(group.DisplayName));
                }
                for (int index = 0; index < settings.ImageProcessingSteps.Count; index++)
                {
                    ImageProcessingStepSettings step = settings.ImageProcessingSteps[index];
                    string keyPrefix = "Step" + (index + 1).ToString(CultureInfo.InvariantCulture);
                    writer.WriteLine("{0}.Id={1}", keyPrefix, Escape(step.Id));
                    writer.WriteLine("{0}.DisplayName={1}", keyPrefix, Escape(step.DisplayName));
                    writer.WriteLine("{0}.GroupId={1}", keyPrefix, Escape(step.GroupId));
                    writer.WriteLine("{0}.Method={1}", keyPrefix, Escape(step.Method));
                    writer.WriteLine("{0}.Parameters={1}", keyPrefix, Escape(step.Parameters));
                }

                writer.WriteLine();
                writer.WriteLine("[ImagePreprocessing]");
                writer.WriteLine("Count={0}", settings.ImagePreprocessingSteps.Count.ToString(CultureInfo.InvariantCulture));
                writer.WriteLine("GroupCount={0}", settings.ImagePreprocessingGroups.Count.ToString(CultureInfo.InvariantCulture));
                for (int index = 0; index < settings.ImagePreprocessingGroups.Count; index++)
                {
                    ImageProcessingGroupSettings group = settings.ImagePreprocessingGroups[index];
                    string keyPrefix = "Group" + (index + 1).ToString(CultureInfo.InvariantCulture);
                    writer.WriteLine("{0}.Id={1}", keyPrefix, Escape(group.Id));
                    writer.WriteLine("{0}.ParentGroupId={1}", keyPrefix, Escape(group.ParentGroupId));
                    writer.WriteLine("{0}.DisplayName={1}", keyPrefix, Escape(group.DisplayName));
                }
                for (int index = 0; index < settings.ImagePreprocessingSteps.Count; index++)
                {
                    ImageProcessingStepSettings step = settings.ImagePreprocessingSteps[index];
                    string keyPrefix = "Step" + (index + 1).ToString(CultureInfo.InvariantCulture);
                    writer.WriteLine("{0}.Id={1}", keyPrefix, Escape(step.Id));
                    writer.WriteLine("{0}.DisplayName={1}", keyPrefix, Escape(step.DisplayName));
                    writer.WriteLine("{0}.GroupId={1}", keyPrefix, Escape(step.GroupId));
                    writer.WriteLine("{0}.Method={1}", keyPrefix, Escape(step.Method));
                    writer.WriteLine("{0}.Parameters={1}", keyPrefix, Escape(step.Parameters));
                }

                writer.WriteLine();
                writer.WriteLine("[ImageRelations]");
                writer.WriteLine("GroupCount={0}", settings.ImageRelationGroups.Count.ToString(CultureInfo.InvariantCulture));
                for (int index = 0; index < settings.ImageRelationGroups.Count; index++)
                {
                    ImageRelationGroupSettings group = settings.ImageRelationGroups[index];
                    string prefix = "Group" + (index + 1).ToString(CultureInfo.InvariantCulture);
                    writer.WriteLine("{0}.Id={1}", prefix, Escape(group.Id));
                    writer.WriteLine("{0}.ParentGroupId={1}", prefix, Escape(group.ParentGroupId));
                    writer.WriteLine("{0}.DisplayName={1}", prefix, Escape(group.DisplayName));
                }
                writer.WriteLine("Count={0}", settings.ImageRelations.Count.ToString(CultureInfo.InvariantCulture));
                for (int index = 0; index < settings.ImageRelations.Count; index++)
                {
                    ImageRelationSettings relation = settings.ImageRelations[index];
                    string prefix = "Relation" + (index + 1).ToString(CultureInfo.InvariantCulture);
                    writer.WriteLine("{0}.Id={1}", prefix, Escape(relation.Id));
                    writer.WriteLine("{0}.DisplayName={1}", prefix, Escape(relation.DisplayName));
                    writer.WriteLine("{0}.SourceType={1}", prefix, Escape(relation.SourceType));
                    writer.WriteLine("{0}.SourceId={1}", prefix, Escape(relation.SourceId));
                    writer.WriteLine("{0}.ProcessingType={1}", prefix, Escape(relation.ProcessingType));
                    writer.WriteLine("{0}.ProcessingId={1}", prefix, Escape(relation.ProcessingId));
                    writer.WriteLine("{0}.GroupId={1}", prefix, Escape(relation.GroupId));
                }

                writer.WriteLine();
                writer.WriteLine("[ObjectJudgement]");
                writer.WriteLine("Count={0}", settings.ObjectJudgements.Count.ToString(CultureInfo.InvariantCulture));
                for (int index = 0; index < settings.ObjectJudgements.Count; index++)
                {
                    ObjectJudgementSettings objectJudgement = settings.ObjectJudgements[index];
                    string prefix = "Object" + (index + 1).ToString(CultureInfo.InvariantCulture);
                    writer.WriteLine("{0}.Id={1}", prefix, Escape(objectJudgement.Id));
                    writer.WriteLine("{0}.DisplayName={1}", prefix, Escape(objectJudgement.DisplayName));
                    writer.WriteLine("{0}.RelationType={1}", prefix, Escape(objectJudgement.RelationType));
                    writer.WriteLine("{0}.RelationId={1}", prefix, Escape(objectJudgement.RelationId));
                    writer.WriteLine("{0}.GroupId={1}", prefix, Escape(objectJudgement.GroupId));
                    writer.WriteLine("{0}.ProcessingCount={1}", prefix, objectJudgement.ProcessingSteps.Count.ToString(CultureInfo.InvariantCulture));
                    for (int processingIndex = 0; processingIndex < objectJudgement.ProcessingSteps.Count; processingIndex++)
                    {
                        ObjectJudgementProcessingSettings processing = objectJudgement.ProcessingSteps[processingIndex];
                        string processingPrefix = prefix + ".Processing" + (processingIndex + 1).ToString(CultureInfo.InvariantCulture);
                        writer.WriteLine("{0}.Id={1}", processingPrefix, Escape(processing.Id));
                        writer.WriteLine("{0}.DisplayName={1}", processingPrefix, Escape(processing.DisplayName));
                        writer.WriteLine("{0}.Method={1}", processingPrefix, Escape(processing.Method));
                        writer.WriteLine("{0}.Parameters={1}", processingPrefix, Escape(processing.Parameters));
                    }
                }

                writer.WriteLine();
                writer.WriteLine("[ObjectJudgementGroups]");
                writer.WriteLine("Count={0}", settings.ObjectJudgementGroups.Count.ToString(CultureInfo.InvariantCulture));
                for (int index = 0; index < settings.ObjectJudgementGroups.Count; index++)
                {
                    ObjectJudgementGroupSettings group = settings.ObjectJudgementGroups[index];
                    string prefix = "Group" + (index + 1).ToString(CultureInfo.InvariantCulture);
                    writer.WriteLine("{0}.Id={1}", prefix, Escape(group.Id));
                    writer.WriteLine("{0}.ParentGroupId={1}", prefix, Escape(group.ParentGroupId));
                    writer.WriteLine("{0}.DisplayName={1}", prefix, Escape(group.DisplayName));
                }

                writer.WriteLine();
                writer.WriteLine("[ObjectDefinition]");
                writer.WriteLine("Count={0}", settings.ObjectDefinitions.Count.ToString(CultureInfo.InvariantCulture));
                for (int index = 0; index < settings.ObjectDefinitions.Count; index++)
                {
                    ObjectDefinitionSettings definition = settings.ObjectDefinitions[index];
                    string prefix = "Definition" + (index + 1).ToString(CultureInfo.InvariantCulture);
                    writer.WriteLine("{0}.Id={1}", prefix, Escape(definition.Id));
                    writer.WriteLine("{0}.DisplayName={1}", prefix, Escape(definition.DisplayName));
                    writer.WriteLine("{0}.SourceType={1}", prefix, Escape(definition.SourceType));
                    writer.WriteLine("{0}.SourceId={1}", prefix, Escape(definition.SourceId));
                    writer.WriteLine("{0}.Connectivity={1}", prefix, definition.Connectivity.ToString(CultureInfo.InvariantCulture));
                    writer.WriteLine("{0}.MinArea={1}", prefix, definition.MinArea.ToString(CultureInfo.InvariantCulture));
                    writer.WriteLine("{0}.MaxArea={1}", prefix, definition.MaxArea.ToString(CultureInfo.InvariantCulture));
                    writer.WriteLine("{0}.NumberingOrder={1}", prefix, Escape(definition.NumberingOrder));
                    writer.WriteLine("{0}.MergeMethod={1}", prefix, Escape(definition.MergeMethod));
                    writer.WriteLine("{0}.MaxMergeDistance={1}", prefix, definition.MaxMergeDistance.ToString(CultureInfo.InvariantCulture));
                    writer.WriteLine("{0}.ResultBoxLineWidth={1}", prefix, definition.ResultBoxLineWidth.ToString(CultureInfo.InvariantCulture));
                    writer.WriteLine("{0}.ResultNumberFontSize={1}", prefix, definition.ResultNumberFontSize.ToString(CultureInfo.InvariantCulture));
                    writer.WriteLine(
                        "{0}.ObjectJudgementIds={1}",
                        prefix,
                        Escape(string.Join("|", definition.ObjectJudgementIds.ToArray())));
                    writer.WriteLine(
                        "{0}.ProcessingCount={1}",
                        prefix,
                        definition.ProcessingSteps.Count.ToString(CultureInfo.InvariantCulture));
                    for (int processingIndex = 0; processingIndex < definition.ProcessingSteps.Count; processingIndex++)
                    {
                        ObjectDefinitionProcessingSettings processing = definition.ProcessingSteps[processingIndex];
                        string processingPrefix = prefix + ".Processing" +
                            (processingIndex + 1).ToString(CultureInfo.InvariantCulture);
                        writer.WriteLine("{0}.Id={1}", processingPrefix, Escape(processing.Id));
                        writer.WriteLine("{0}.DisplayName={1}", processingPrefix, Escape(processing.DisplayName));
                        writer.WriteLine("{0}.Method={1}", processingPrefix, Escape(processing.Method));
                        writer.WriteLine("{0}.Parameters={1}", processingPrefix, Escape(processing.Parameters));
                    }
                }
            }
        }

        private static void EnsureAllEntityIds(SystemParameterSettings settings)
        {
            EnsureRoiIds(settings.RoiRegions);
            EnsureStepIds(settings.ImageProcessingSteps);
            EnsureStepIds(settings.ImagePreprocessingSteps);
            EnsureImageProcessingGroupIds(settings.ImageProcessingGroups);
            EnsureImageProcessingGroupIds(settings.ImagePreprocessingGroups);
            EnsureImageRelationGroupIds(settings.ImageRelationGroups);
            EnsureImageRelationIds(settings.ImageRelations);
            EnsureObjectJudgementGroupIds(settings.ObjectJudgementGroups);

            var objectIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (ObjectJudgementSettings objectJudgement in settings.ObjectJudgements)
            {
                objectJudgement.Id = EnsureUniqueId(objectJudgement.Id, objectIds);
                EnsureObjectJudgementProcessingIds(objectJudgement.ProcessingSteps);
            }

            var definitionIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (ObjectDefinitionSettings definition in settings.ObjectDefinitions)
            {
                definition.Id = EnsureUniqueId(definition.Id, definitionIds);
                EnsureObjectDefinitionProcessingIds(definition.ProcessingSteps);
            }

            RepairReferences(settings);
        }

        private static void EnsureObjectDefinitionProcessingIds(
            List<ObjectDefinitionProcessingSettings> processingSteps)
        {
            var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (ObjectDefinitionProcessingSettings processing in processingSteps)
            {
                processing.Id = EnsureUniqueId(processing.Id, ids);
            }
        }

        private static void EnsureRoiIds(List<RoiRegionSettings> rois)
        {
            var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (RoiRegionSettings roi in rois)
            {
                roi.Id = EnsureUniqueId(roi.Id, ids);
            }
        }

        private static void EnsureStepIds(List<ImageProcessingStepSettings> steps)
        {
            var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (ImageProcessingStepSettings step in steps)
            {
                step.Id = EnsureUniqueId(step.Id, ids);
            }
        }

        private static void EnsureImageProcessingGroupIds(List<ImageProcessingGroupSettings> groups)
        {
            var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (ImageProcessingGroupSettings group in groups)
            {
                group.Id = EnsureUniqueId(group.Id, ids);
            }
        }

        private static void EnsureImageRelationGroupIds(List<ImageRelationGroupSettings> groups)
        {
            var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (ImageRelationGroupSettings group in groups)
            {
                group.Id = EnsureUniqueId(group.Id, ids);
            }
        }

        private static void EnsureImageRelationIds(List<ImageRelationSettings> relations)
        {
            var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (ImageRelationSettings relation in relations)
            {
                relation.Id = EnsureUniqueId(relation.Id, ids);
            }
        }

        private static void EnsureObjectJudgementGroupIds(List<ObjectJudgementGroupSettings> groups)
        {
            var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (ObjectJudgementGroupSettings group in groups)
            {
                group.Id = EnsureUniqueId(group.Id, ids);
            }
        }

        private static void EnsureObjectJudgementProcessingIds(
            List<ObjectJudgementProcessingSettings> processingSteps)
        {
            var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (ObjectJudgementProcessingSettings processing in processingSteps)
            {
                processing.Id = EnsureUniqueId(processing.Id, ids);
            }
        }

        private static string EnsureUniqueId(string id, HashSet<string> usedIds)
        {
            string normalizedId = id == null ? string.Empty : id.Trim();
            if (normalizedId.Length == 0 || usedIds.Contains(normalizedId))
            {
                do
                {
                    normalizedId = Guid.NewGuid().ToString("N");
                }
                while (usedIds.Contains(normalizedId));
            }

            usedIds.Add(normalizedId);
            return normalizedId;
        }

        private static void RepairReferences(SystemParameterSettings settings)
        {
            var preprocessingStepIds = new HashSet<string>(
                settings.ImagePreprocessingSteps.ConvertAll(step => step.Id),
                StringComparer.Ordinal);
            var preprocessingGroupIds = new HashSet<string>(
                settings.ImagePreprocessingGroups.ConvertAll(group => group.Id),
                StringComparer.Ordinal);
            var processingStepIds = new HashSet<string>(
                settings.ImageProcessingSteps.ConvertAll(step => step.Id),
                StringComparer.Ordinal);
            var processingGroupIds = new HashSet<string>(
                settings.ImageProcessingGroups.ConvertAll(group => group.Id),
                StringComparer.Ordinal);
            var relationIds = new HashSet<string>(
                settings.ImageRelations.ConvertAll(relation => relation.Id),
                StringComparer.Ordinal);
            var relationGroupIds = new HashSet<string>(
                settings.ImageRelationGroups.ConvertAll(group => group.Id),
                StringComparer.Ordinal);
            var objectJudgementIds = new HashSet<string>(
                settings.ObjectJudgements.ConvertAll(objectJudgement => objectJudgement.Id),
                StringComparer.Ordinal);
            var objectJudgementGroupIds = new HashSet<string>(
                settings.ObjectJudgementGroups.ConvertAll(group => group.Id),
                StringComparer.Ordinal);

            foreach (ImageProcessingStepSettings step in settings.ImageProcessingSteps)
            {
                if (!string.IsNullOrWhiteSpace(step.GroupId) &&
                    !processingGroupIds.Contains(step.GroupId))
                {
                    step.GroupId = string.Empty;
                }
            }

            foreach (ImageProcessingStepSettings step in settings.ImagePreprocessingSteps)
            {
                if (!string.IsNullOrWhiteSpace(step.GroupId) &&
                    !preprocessingGroupIds.Contains(step.GroupId))
                {
                    step.GroupId = string.Empty;
                }
            }

            foreach (ImageProcessingGroupSettings group in settings.ImageProcessingGroups)
            {
                if (!string.IsNullOrWhiteSpace(group.ParentGroupId) &&
                    !processingGroupIds.Contains(group.ParentGroupId))
                {
                    group.ParentGroupId = string.Empty;
                }
            }

            foreach (ImageProcessingGroupSettings group in settings.ImagePreprocessingGroups)
            {
                if (!string.IsNullOrWhiteSpace(group.ParentGroupId) &&
                    !preprocessingGroupIds.Contains(group.ParentGroupId))
                {
                    group.ParentGroupId = string.Empty;
                }
            }

            foreach (ImageRelationGroupSettings group in settings.ImageRelationGroups)
            {
                if (!string.IsNullOrWhiteSpace(group.ParentGroupId) &&
                    !relationGroupIds.Contains(group.ParentGroupId))
                {
                    group.ParentGroupId = string.Empty;
                }
            }

            foreach (ImageRelationSettings relation in settings.ImageRelations)
            {
                if (string.Equals(relation.SourceType, "Step", StringComparison.Ordinal) &&
                    !preprocessingStepIds.Contains(relation.SourceId))
                {
                    relation.SourceId = string.Empty;
                }
                else if (string.Equals(relation.SourceType, "Group", StringComparison.Ordinal) &&
                    !preprocessingGroupIds.Contains(relation.SourceId))
                {
                    relation.SourceId = string.Empty;
                }
                else if (string.Equals(relation.SourceType, "Original", StringComparison.Ordinal))
                {
                    relation.SourceId = string.Empty;
                }

                if (string.Equals(relation.ProcessingType, "Step", StringComparison.Ordinal) &&
                    !processingStepIds.Contains(relation.ProcessingId))
                {
                    relation.ProcessingId = string.Empty;
                }
                else if (string.Equals(relation.ProcessingType, "Group", StringComparison.Ordinal) &&
                    !processingGroupIds.Contains(relation.ProcessingId))
                {
                    relation.ProcessingId = string.Empty;
                }

                if (!string.IsNullOrWhiteSpace(relation.GroupId) &&
                    !relationGroupIds.Contains(relation.GroupId))
                {
                    relation.GroupId = string.Empty;
                }
            }

            foreach (ObjectJudgementSettings objectJudgement in settings.ObjectJudgements)
            {
                if (string.Equals(objectJudgement.RelationType, "Relation", StringComparison.Ordinal) &&
                    !relationIds.Contains(objectJudgement.RelationId))
                {
                    objectJudgement.RelationType = string.Empty;
                    objectJudgement.RelationId = string.Empty;
                }
                else if (string.Equals(objectJudgement.RelationType, "Group", StringComparison.Ordinal) &&
                    !relationGroupIds.Contains(objectJudgement.RelationId))
                {
                    objectJudgement.RelationType = string.Empty;
                    objectJudgement.RelationId = string.Empty;
                }

                if (!string.IsNullOrWhiteSpace(objectJudgement.GroupId) &&
                    !objectJudgementGroupIds.Contains(objectJudgement.GroupId))
                {
                    objectJudgement.GroupId = string.Empty;
                }
            }

            foreach (ObjectJudgementGroupSettings group in settings.ObjectJudgementGroups)
            {
                if (!string.IsNullOrWhiteSpace(group.ParentGroupId) &&
                    !objectJudgementGroupIds.Contains(group.ParentGroupId))
                {
                    group.ParentGroupId = string.Empty;
                }
            }

            foreach (ObjectDefinitionSettings definition in settings.ObjectDefinitions)
            {
                definition.ObjectJudgementIds.RemoveAll(id => !objectJudgementIds.Contains(id));
                if (string.Equals(definition.SourceType, "ObjectJudgement", StringComparison.Ordinal) &&
                    !string.IsNullOrWhiteSpace(definition.SourceId) &&
                    !objectJudgementIds.Contains(definition.SourceId))
                {
                    definition.SourceId = string.Empty;
                }
                else if (string.Equals(definition.SourceType, "Group", StringComparison.Ordinal) &&
                    !string.IsNullOrWhiteSpace(definition.SourceId) &&
                    !objectJudgementGroupIds.Contains(definition.SourceId))
                {
                    definition.SourceId = string.Empty;
                }

                if (definition.Connectivity != 4 && definition.Connectivity != 8)
                {
                    definition.Connectivity = 8;
                }

                if (definition.MinArea < 0)
                {
                    definition.MinArea = 0;
                }

                if (definition.MaxArea < 0)
                {
                    definition.MaxArea = 0;
                }

                if (definition.MaxMergeDistance < 0)
                {
                    definition.MaxMergeDistance = 0;
                }

                definition.ResultBoxLineWidth = Math.Max(1, Math.Min(20, definition.ResultBoxLineWidth));
                definition.ResultNumberFontSize = Math.Max(6, Math.Min(72, definition.ResultNumberFontSize));
            }
        }

        private Dictionary<string, Dictionary<string, string>> ReadSections()
        {
            var sections = new Dictionary<string, Dictionary<string, string>>(StringComparer.OrdinalIgnoreCase);
            if (!File.Exists(filePath))
            {
                return sections;
            }

            string currentSection = string.Empty;
            foreach (string rawLine in File.ReadAllLines(filePath))
            {
                string line = rawLine.Trim();
                if (line.Length == 0 || line.StartsWith(";", StringComparison.Ordinal) || line.StartsWith("#", StringComparison.Ordinal))
                {
                    continue;
                }

                if (line.StartsWith("[", StringComparison.Ordinal) && line.EndsWith("]", StringComparison.Ordinal))
                {
                    currentSection = line.Substring(1, line.Length - 2).Trim();
                    if (!sections.ContainsKey(currentSection))
                    {
                        sections[currentSection] = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                    }

                    continue;
                }

                int separator = line.IndexOf('=');
                if (separator <= 0)
                {
                    continue;
                }

                if (!sections.ContainsKey(currentSection))
                {
                    sections[currentSection] = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                }

                string key = line.Substring(0, separator).Trim();
                string value = line.Substring(separator + 1).Trim();
                sections[currentSection][key] = value;
            }

            return sections;
        }

        private static string GetValue(Dictionary<string, Dictionary<string, string>> sections, string section, string key, string defaultValue)
        {
            Dictionary<string, string> values;
            string value;
            return sections.TryGetValue(section, out values) && values.TryGetValue(key, out value)
                ? Unescape(value)
                : defaultValue;
        }

        private static int GetInt(Dictionary<string, Dictionary<string, string>> sections, string section, string key, int defaultValue)
        {
            int value;
            return int.TryParse(GetValue(sections, section, key, string.Empty), NumberStyles.Integer, CultureInfo.InvariantCulture, out value)
                ? value
                : defaultValue;
        }

        private static double GetDouble(Dictionary<string, Dictionary<string, string>> sections, string section, string key, double defaultValue)
        {
            double value;
            return double.TryParse(GetValue(sections, section, key, string.Empty), NumberStyles.Float, CultureInfo.InvariantCulture, out value)
                ? value
                : defaultValue;
        }

        private static bool GetBool(Dictionary<string, Dictionary<string, string>> sections, string section, string key, bool defaultValue)
        {
            bool value;
            return bool.TryParse(GetValue(sections, section, key, string.Empty), out value)
                ? value
                : defaultValue;
        }

        private static string Escape(string value)
        {
            return (value ?? string.Empty).Replace("\\", "\\\\").Replace("\r", "\\r").Replace("\n", "\\n");
        }

        private static string Unescape(string value)
        {
            return (value ?? string.Empty).Replace("\\n", "\n").Replace("\\r", "\r").Replace("\\\\", "\\");
        }
    }
}
