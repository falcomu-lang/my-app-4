using System.Drawing;

namespace IntegratedImageProcessingApp.Services
{
    public class SystemParameterSettings
    {
        public string LastImagePath { get; set; }

        public bool RoiEnabled { get; set; }

        public Rectangle Roi { get; set; }
    }
}
