using System.Windows.Media;

namespace WannaDoWidget
{
    public class NoClipGrid : System.Windows.Controls.Grid
    {
        protected override Geometry? GetLayoutClip(System.Windows.Size layoutSlotSize) => null;
    }

    public class NoClipListBox : System.Windows.Controls.ListBox
    {
        protected override Geometry? GetLayoutClip(System.Windows.Size layoutSlotSize) => null;
    }

    public class NoClipItemsPresenter : System.Windows.Controls.ItemsPresenter
    {
        protected override Geometry? GetLayoutClip(System.Windows.Size layoutSlotSize) => null;
    }

    public class NoClipStackPanel : System.Windows.Controls.StackPanel
    {
        protected override Geometry? GetLayoutClip(System.Windows.Size layoutSlotSize) => null;
    }
}
