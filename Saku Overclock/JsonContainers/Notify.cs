using Microsoft.UI.Xaml.Controls;

namespace Saku_Overclock.JsonContainers;
public class Notify
{
    public string Title { get; set; } = "";
    public string Msg { get; set; } = "";
    public InfoBarSeverity Type { get; set; } = InfoBarSeverity.Informational; 
    public bool IsClosable { get; set; } = true; 
}