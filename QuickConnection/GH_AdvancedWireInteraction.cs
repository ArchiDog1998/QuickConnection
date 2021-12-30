using Grasshopper;
using Grasshopper.GUI;
using Grasshopper.GUI.Canvas;
using Grasshopper.GUI.Canvas.Interaction;
using Grasshopper.Kernel;
using System;
using System.Drawing;
using System.Linq;
using System.Reflection;
using System.Windows.Forms;

namespace QuickConnection
{
    public class GH_AdvancedWireInteraction : GH_WireInteraction
    {
        internal static readonly FieldInfo _sourceInfo = typeof(GH_WireInteraction).GetRuntimeFields().Where(m => m.Name.Contains("m_source")).First();
        private static readonly FieldInfo _targetInfo = typeof(GH_WireInteraction).GetRuntimeFields().Where(m => m.Name.Contains("m_target")).First();
        private static readonly FieldInfo _fromInputInfo = typeof(GH_WireInteraction).GetRuntimeFields().Where(m => m.Name.Contains("m_dragfrominput")).First();
        private static readonly FieldInfo _pointInfo = typeof(GH_WireInteraction).GetRuntimeFields().Where(m => m.Name.Contains("m_point")).First();

        private static float _screenScale = 0f;

        internal static float screenScale
        {
            get
            {
                if (_screenScale == 0f)
                {
                    _screenScale = Graphics.FromHwnd(IntPtr.Zero).DpiX / 96;
                }
                return _screenScale;
            }
        }
        internal static PointF _lastCanvasLoacation = PointF.Empty;

        public GH_AdvancedWireInteraction(GH_Canvas iParent, GH_CanvasMouseEvent mEvent, IGH_Param Source)
            : base(iParent, mEvent, Source)
        {
            if(_lastCanvasLoacation != PointF.Empty)
            {
                _pointInfo.SetValue(this, _lastCanvasLoacation);
                iParent.Refresh();
            }
        }

        public override GH_ObjectResponse RespondToMouseMove(GH_Canvas sender, GH_CanvasMouseEvent e)
        {
            if (_lastCanvasLoacation != PointF.Empty)
                return GH_ObjectResponse.Handled;
            return base.RespondToMouseMove(sender, e);

        }

        public override GH_ObjectResponse RespondToMouseDown(GH_Canvas sender, GH_CanvasMouseEvent e)
        {
            if (e.Button == MouseButtons.Right)
                return base.RespondToMouseDown(sender, e);
            else return RespondToMouseUp(sender,e);
        }

        public override GH_ObjectResponse RespondToMouseUp(GH_Canvas sender, GH_CanvasMouseEvent e)
        {
            IGH_Param source = (IGH_Param)_sourceInfo.GetValue(this);

            if (DistanceTo(e.CanvasLocation, CanvasPointDown) < 10)
            {
                return GH_ObjectResponse.Ignore;
            }
            else if (e.Button != MouseButtons.Left)
            {
                Instances.ActiveCanvas.ActiveInteraction = null;
                return GH_ObjectResponse.Ignore;
            }
            else if (e.Button == MouseButtons.Left && _targetInfo.GetValue(this) == null && !source.Attributes.GetTopLevel.Bounds.Contains(e.CanvasLocation))
            {
                Point location = Instances.ActiveCanvas.PointToScreen(e.ControlLocation);

                new ChooseWindow(source, (bool)_fromInputInfo.GetValue(this), e.CanvasLocation)
                {
                    WindowStartupLocation = System.Windows.WindowStartupLocation.Manual,
                    Left = location.X / screenScale,
                    Top = location.Y / screenScale,
                    Width = QuickConnectionAssemblyLoad.QuickConnectionWindowWidth,
                    Height = QuickConnectionAssemblyLoad.QuickConnectionWindowHeight,
                }.Show();

                _lastCanvasLoacation = e.CanvasLocation;

                return GH_ObjectResponse.Handled;
            }
            return base.RespondToMouseUp(sender, e);
        }

        private static float DistanceTo(PointF a, PointF b)
        {
            return (float)Math.Sqrt(Math.Pow(a.X - b.X, 2) + Math.Pow(a.Y - b.Y, 2));
        }
    }
}
