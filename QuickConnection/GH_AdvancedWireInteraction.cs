﻿using Grasshopper;
using Grasshopper.GUI;
using Grasshopper.GUI.Canvas;
using Grasshopper.GUI.Canvas.Interaction;
using Grasshopper.Kernel;
using System;
using System.Drawing;
using System.Linq;
using System.Reflection;
using System.Windows.Forms;

namespace QuickConnection;

public class GH_AdvancedWireInteraction : GH_WireInteraction
{
    internal static readonly FieldInfo _sourceInfo = typeof(GH_WireInteraction).GetRuntimeFields().First(m => m.Name.Contains("m_source"));
    private static readonly FieldInfo _targetInfo = typeof(GH_WireInteraction).GetRuntimeFields().First(m => m.Name.Contains("m_target"));

    /// <summary>
    /// The mouse down location when this is created.
    /// </summary>
    private static readonly FieldInfo _fromInputInfo = typeof(GH_WireInteraction).GetRuntimeFields().First(m => m.Name.Contains("m_dragfrominput"));

    /// <summary>
    /// The point location right now.
    /// </summary>
    private static readonly FieldInfo _pointInfo = typeof(GH_WireInteraction).GetRuntimeFields().First(m => m.Name.Contains("m_point"));

    private static readonly MethodInfo _fixCursor = typeof(GH_WireInteraction).GetRuntimeMethods().First(m => m.Name.Contains("FixCursor"));
    private static readonly MethodInfo _performWire = typeof(GH_WireInteraction).GetRuntimeMethods().First(m => m.Name.Contains("PerformWireOperation"));

    private static float _screenScale = 0f;
    internal static PointF _click = PointF.Empty;
    internal static float ScreenScale
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


    private GH_PanInteraction _panInteraction;
    private Point _panControlLocation;

    private readonly DateTime _time;
    private bool _isFirstUp = true;
    public GH_AdvancedWireInteraction(GH_Canvas iParent, GH_CanvasMouseEvent mEvent, IGH_Param Source)
        : base(iParent, mEvent, Source)
    {
        if (_lastCanvasLoacation != PointF.Empty)
        {
            _pointInfo.SetValue(this, _lastCanvasLoacation);
            iParent.Refresh();
        }
        _time = DateTime.Now;

        if (Source.GetType().FullName.Contains("Grasshopper.Kernel.Components.GH_PlaceholderParameter") && !Source.Attributes.IsTopLevel)
        {
            if(Source.Attributes.GetTopLevel.DocObject is GH_Component parant)
            {
                _fromInputInfo.SetValue(this, parant.Params.Input.Contains(Source));
            }
        }
    }

    public override GH_ObjectResponse RespondToMouseMove(GH_Canvas sender, GH_CanvasMouseEvent e)
    {
        //Make sure when window open, the preview is stable.
        if (_lastCanvasLoacation != PointF.Empty)
            return GH_ObjectResponse.Handled;

        _fixCursor.Invoke(this, []);

        GH_Document document = sender.Document;
        if (document == null)
        {
            return GH_ObjectResponse.Ignore;
        }

        _panInteraction?.RespondToMouseMove(sender, e);

        _pointInfo.SetValue(this, e.CanvasLocation);
        _targetInfo.SetValue(this, null);

        bool fromInput = (bool)_fromInputInfo.GetValue(this);

        IGH_Attributes iGH_Attributes = document.FindAttributeByGrip(e.CanvasLocation, bLimitToOutside: false, !fromInput, fromInput, 20);

        //Closest Attribute.
        iGH_Attributes ??= GetRightAttribute(document, e, fromInput);

        if (SimpleAssemblyPriority.CantWireEasily) base.RespondToMouseMove(sender, e);


        if (iGH_Attributes == null)
        {
            base.Canvas.Refresh();
            return GH_ObjectResponse.Handled;
        }

        if (iGH_Attributes.GetTopLevel.DocObject == ((IGH_Param)_sourceInfo.GetValue(this)).Attributes.GetTopLevel.DocObject)
        {
            base.Canvas.Refresh();
            return GH_ObjectResponse.Handled;
        }
        if (iGH_Attributes.DocObject is not IGH_Param)
        {
            base.Canvas.Refresh();
            return GH_ObjectResponse.Handled;
        }
        _targetInfo.SetValue(this, (IGH_Param)iGH_Attributes.DocObject);

        if (fromInput)
        {
            _pointInfo.SetValue(this, iGH_Attributes.OutputGrip);
        }
        else
        {
            _pointInfo.SetValue(this, iGH_Attributes.InputGrip);
        }
        base.Canvas.Refresh();
        return GH_ObjectResponse.Handled;
    }

    private static float Distance(PointF point1, PointF point2)
    {
        return (float)Math.Sqrt(Math.Pow(point1.X - point2.X, 2) + Math.Pow(point1.Y - point2.Y, 2));
    }

    public override GH_ObjectResponse RespondToMouseDown(GH_Canvas sender, GH_CanvasMouseEvent e)
    {
        if (e.Button == MouseButtons.Right)
        {
            _panInteraction = new GH_PanInteraction(sender, e);
            _panControlLocation = e.ControlLocation;
            return base.RespondToMouseDown(sender, e);
        }
        else
            return RespondToMouseUp(sender, e);
    }

    public override GH_ObjectResponse RespondToMouseUp(GH_Canvas sender, GH_CanvasMouseEvent e)
    {
        if (!Canvas.IsDocument)
        {
            return GH_ObjectResponse.Release;
        }

        IGH_Param source = (IGH_Param)_sourceInfo.GetValue(this);
        bool notHoldKeys = Control.ModifierKeys == Keys.None;
        bool inTime = _isFirstUp && (DateTime.Now - _time).TotalMilliseconds < SimpleAssemblyPriority.QuickConnectionMaxWaitTime;
        _isFirstUp = false;

        if (e.Button != MouseButtons.Left)
        {
            if (_panInteraction != null && DistanceTo(e.ControlLocation, _panControlLocation) < 10)
            {
                Instances.ActiveCanvas.ActiveInteraction = null;
            }

            _panInteraction = null;
            return GH_ObjectResponse.Ignore;
        }

        //If the wire is connected than return.
        else  if (((IGH_Param)_targetInfo.GetValue(this)) != null)
        {
            _click = e.CanvasLocation;
            if (SimpleAssemblyPriority.CantWireEasily)
            {
                return base.RespondToMouseUp(sender, e);
            }
            else
            {
                _performWire.Invoke(this, new object[] { GH_ObjectResponse.Release });
                sender.Document.NewSolution(false);
                return GH_ObjectResponse.Release;
            }
        }

        //Make Click To Enable Interaction.
        else if (inTime || DistanceTo(e.CanvasLocation, CanvasPointDown) < 10)
        {
            return GH_ObjectResponse.Ignore;
        }
        //Open the Operation Window.
        else if (notHoldKeys && !source.Attributes.GetTopLevel.Bounds.Contains(e.CanvasLocation))
        {
            Point location = Instances.ActiveCanvas.PointToScreen(e.ControlLocation);

            new ChooseWindow(source, (bool)_fromInputInfo.GetValue(this), e.CanvasLocation)
            {
                WindowStartupLocation = System.Windows.WindowStartupLocation.Manual,
                Left = location.X / ScreenScale,
                Top = location.Y / ScreenScale,
                Width = SimpleAssemblyPriority.QuickConnectionWindowWidth,
                Height = SimpleAssemblyPriority.QuickConnectionWindowHeight,
            }.Show();

            _lastCanvasLoacation = e.CanvasLocation;

            return GH_ObjectResponse.Handled;
        }
        return base.RespondToMouseUp(sender, e);
    }

    internal static IGH_Attributes GetRightAttribute(GH_Document document, GH_CanvasMouseEvent e, bool input)
    {
        if (!SimpleAssemblyPriority.UseFuzzyConnection) return null;
        IGH_Attributes iGH_Attributes = null;

        IGH_Attributes attr = document.FindAttribute(e.CanvasLocation, true);
        if(attr == null) return null;

        IGH_DocumentObject obj = attr.DocObject;
        if (obj == null) return null;

        if (obj is IGH_Param)
        {
            if (input && attr.HasOutputGrip)
            {
                iGH_Attributes = attr;
            }
            else if (!input && attr.HasInputGrip)
            {
                iGH_Attributes = attr;
            }
        }
        else if (obj is IGH_Component com)
        {
            float minDis = float.MaxValue;
            if (input)
            {
                foreach (IGH_Param param in com.Params.Output)
                {
                    float dis = Distance(param.Attributes.OutputGrip, e.CanvasLocation);
                    if (dis < minDis)
                    {
                        minDis = dis;
                        iGH_Attributes = param.Attributes;
                    }
                }
            }
            else
            {
                foreach (IGH_Param param in com.Params.Input)
                {
                    float dis = Distance(param.Attributes.InputGrip, e.CanvasLocation);
                    if (dis < minDis)
                    {
                        minDis = dis;
                        iGH_Attributes = param.Attributes;
                    }
                }
            }
        }

        return iGH_Attributes;

    }

    internal static float DistanceTo(PointF a, PointF b)
    {
        return (float)Math.Sqrt(Math.Pow(a.X - b.X, 2) + Math.Pow(a.Y - b.Y, 2));
    }
}
