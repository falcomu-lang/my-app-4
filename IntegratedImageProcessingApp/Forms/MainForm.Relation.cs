using System;
using System.Drawing;
using System.Windows.Forms;

namespace IntegratedImageProcessingApp.Forms
{
    public partial class MainForm
    {
        private int selectedImageRelationIndex = -1;
        private Panel imageRelationParameterPanel;
        private string activeImageRelationSourceType;
        private string activeImageRelationSourceId;
        private const string ImageRelationMenuText = "影像關聯";

        private sealed class RelationChoice
        {
            public string DisplayText { get; set; }
            public string Type { get; set; }
            public string Id { get; set; }
            public override string ToString() { return DisplayText; }
        }
    }
}
