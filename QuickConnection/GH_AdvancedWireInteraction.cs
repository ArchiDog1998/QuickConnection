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

namespace QuickConnection
{
    public class GH_AdvancedWireInteraction : GH_WireInteraction
    {
        internal static readonly FieldInfo _sourceInfo = typeof(GH_WireInteraction).GetRuntimeFields().Where(m => m.Name.Contains("m_source")).First();
        private static readonly FieldInfo _targetInfo = typeof(GH_WireInteraction).GetRuntimeFields().Where(m => m.Name.Contains("m_target")).First();

        /// <summary>
        /// The mouse down location when this is created.
        /// </summary>
        private static readonly FieldInfo _fromInputInfo = typeof(GH_WireInteraction).GetRuntimeFields().Where(m => m.Name.Contains("m_dragfrominput")).First();

        /// <summary>
        /// The point location right now.
        /// </summary>
        private static readonly FieldInfo _pointInfo = typeof(GH_WireInteraction).GetRuntimeFields().Where(m => m.Name.Contains("m_point")).First();

        private static readonly MethodInfo _fixCursor = typeof(GH_WireInteraction).GetRuntimeMethods().Where(m => m.Name.Contains("FixCursor")).First();

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
            //m_active = true;
        }

        public override GH_ObjectResponse RespondToMouseMove(GH_Canvas sender, GH_CanvasMouseEvent e)
        {
            //Make sure when window open, the preview is stable.
            if (_lastCanvasLoacation != PointF.Empty)
                return GH_ObjectResponse.Handled;

            base.RespondToMouseMove(sender, e);

            _fixCursor.Invoke(this, new object[] { });

            GH_Document document = sender.Document;
            if (document == null)
            {
                return GH_ObjectResponse.Ignore;
            }
            _pointInfo.SetValue(this, e.CanvasLocation);
            _targetInfo.SetValue(this, null);

            bool fromInput = (bool)_fromInputInfo.GetValue(this);

            IGH_Attributes iGH_Attributes = document.FindAttributeByGrip(e.CanvasLocation, bLimitToOutside: false, !fromInput, fromInput, 20);

            //Closest Attribute.
            if (QuickConnectionAssemblyLoad.UseFuzzyConnection && iGH_Attributes == null)
            {
                IGH_Attributes attr = document.FindAttribute(e.CanvasLocation, true);
                if (attr != null)
                {
                    IGH_DocumentObject obj = attr.DocObject;
                    if (obj != null)
                    {
                        if (obj is IGH_Param)
                        {
                            if (fromInput && attr.HasOutputGrip)
                            {
                                iGH_Attributes = attr;
                            }
                            else if (!fromInput && attr.HasInputGrip)
                            {
                                iGH_Attributes = attr;
                            }
                        }
                        else if (obj is IGH_Component)
                        {
                            IGH_Component com = (IGH_Component)obj;
                            float minDis = float.MaxValue;
                            if (fromInput)
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
                    }
                }
            }

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
            if (!(iGH_Attributes.DocObject is IGH_Param))
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
                return base.RespondToMouseDown(sender, e);
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

            //If the wire is connected than return.
            if (((IGH_Param)_targetInfo.GetValue(this)) != null)
            {
                return base.RespondToMouseUp(sender, e);
            }
            //End the Interaction if indeed.
            else if (e.Button != MouseButtons.Left)
            {
                Instances.ActiveCanvas.ActiveInteraction = null;
                return GH_ObjectResponse.Ignore;
            }
            //Make Click To Enable Interaction.
            else if (DistanceTo(e.CanvasLocation, CanvasPointDown) < 10 && notHoldKeys)
            {
                return GH_ObjectResponse.Ignore;
            }
            //Open the Operation Window.
            else if (notHoldKeys &&
                !source.Attributes.GetTopLevel.Bounds.Contains(e.CanvasLocation))
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
