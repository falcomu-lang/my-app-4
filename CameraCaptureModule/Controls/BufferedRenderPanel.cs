using System.Windows.Forms;

namespace CameraCaptureApp.Controls
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
