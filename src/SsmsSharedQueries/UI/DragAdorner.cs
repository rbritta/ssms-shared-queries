using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;

namespace SsmsSharedQueries.UI
{
    /// <summary>A translucent "ghost" that follows the cursor during a manual drag.</summary>
    internal sealed class DragAdorner : Adorner
    {
        private readonly ContentPresenter _content;
        private readonly AdornerLayer _layer;
        private double _left, _top;

        public DragAdorner(UIElement adorned, UIElement child, AdornerLayer layer) : base(adorned)
        {
            _layer = layer;
            _content = new ContentPresenter { Content = child, Opacity = 0.8, IsHitTestVisible = false };
            IsHitTestVisible = false;
            _layer.Add(this);
        }

        public void UpdatePosition(double left, double top)
        {
            _left = left;
            _top = top;
            _layer?.Update(AdornedElement);
        }

        public void Detach() => _layer?.Remove(this);

        protected override int VisualChildrenCount => 1;
        protected override Visual GetVisualChild(int index) => _content;
        protected override Size MeasureOverride(Size constraint) { _content.Measure(constraint); return _content.DesiredSize; }
        protected override Size ArrangeOverride(Size finalSize) { _content.Arrange(new Rect(_content.DesiredSize)); return finalSize; }

        public override GeneralTransform GetDesiredTransform(GeneralTransform transform)
        {
            var group = new GeneralTransformGroup();
            group.Children.Add(base.GetDesiredTransform(transform));
            group.Children.Add(new TranslateTransform(_left + 14, _top + 4));
            return group;
        }
    }
}
