using ESC_POS_USB_NET.Enums;
using System.Drawing;

namespace ESC_POS_USB_NET.Interfaces.Command
{
    internal interface IImage
    {
        byte[] Print(byte[] ImageData, bool isScale, HorizonalAlignment alignment);
    }
}
