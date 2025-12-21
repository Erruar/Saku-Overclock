using System;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Animation;

namespace Saku_Overclock.Styles;
public sealed class CopyButton : Button
{
    public CopyButton()
    {
        DefaultStyleKey = typeof(CopyButton);
        Translation = new System.Numerics.Vector3(0, 0, 20);
    }

    private void CopyButton_Click(object sender, RoutedEventArgs e)
    {
        if (GetTemplateChild("CopyToClipboardSuccessAnimation") is Storyboard storyBoard)
        {
            storyBoard.Begin(); 
        }
    }

    protected override void OnApplyTemplate()
    {
        Click -= CopyButton_Click;
        base.OnApplyTemplate();
        Click += CopyButton_Click;
    }
}
