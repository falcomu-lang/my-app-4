using System.Windows.Forms;

namespace IntegratedImageProcessingApp.Controls
{
    public class BufferedRenderPanel : Panel
    {
        public BufferedRenderPanel()
        {
            DoubleBuffered = true;
            ResizeRedraw = true;
        }
    }
}
