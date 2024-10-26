using Microsoft.UI.Xaml.Controls;
using Windows.Foundation;

namespace Saku_Overclock.SMUEngine;
public class Notify
{
    public string Title { get; set; } = "";
    public string Msg { get; set; } = "";
    public InfoBarSeverity Type { get; set; } = InfoBarSeverity.Informational; 
    public bool isClosable { get; set; } = true; 
}