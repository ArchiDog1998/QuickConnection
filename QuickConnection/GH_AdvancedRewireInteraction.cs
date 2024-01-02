using Grasshopper;
using Grasshopper.GUI;
using Grasshopper.GUI.Canvas;
using Grasshopper.GUI.Canvas.Interaction;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Undo;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Reflection;
using System.Windows.Forms;

namespace QuickConnection;

internal class GH_AdvancedRewireInteraction : GH_RewireInteraction
{
    private static readonly FieldInfo _paramInfo = typeof(GH_RewireInteraction).GetRuntimeFields().First(m => m.Name.Contains("m_params"));
    internal static readonly FieldInfo _sourceInfo = typeof(GH_RewireInteraction).GetRuntimeFields().First(m => m.Name.Contains("m_source"));
    private static readonly FieldInfo _targetInfo = typeof(GH_RewireInteraction).GetRuntimeFields().First(m => m.Name.Contains("m_target"));
    private static readonly FieldInfo _inputInfo = typeof(GH_RewireInteraction).GetRuntimeFields().First(m => m.Name.Contains("m_input"));
    private static readonly FieldInfo _pointInfo = typeof(GH_RewireInteraction).GetRuntimeFields().First(m => m.Name.Contains("m_point"));
    private static readonly FieldInfo _paramsInfo = typeof(GH_RewireInteraction).GetRuntimeFields().First(m => m.Name.Contains("m_params"));
    private static readonly FieldInfo _GripInfo = typeof(GH_RewireInteraction).GetRuntimeFields().First(m => m.Name.Contains("m_grips"));

    private GH_PanInteraction _panInteraction;
    private Point _panControlLocation;

    private readonly Stopwatch _timer;
    private bool _isFirstUp = true;

    public GH_AdvancedRewireInteraction(GH_Canvas iParent, GH_CanvasMouseEvent mEvent, IGH_Param Source)
        :base(iParent, mEvent, Source)
    {
        _timer = new Stopwatch();
        _timer.Start();
    }

    public override GH_ObjectResponse RespondToMouseMove(GH_Canvas sender, GH_CanvasMouseEvent e)
    {
        //base.RespondToMouseMove(sender, e);
        _pointInfo.SetValue(this, e.CanvasLocation);
        GH_Document document = sender.Document;
        if (document == null)
        {
            return GH_ObjectResponse.Ignore;
        }

        if(_panInteraction != null)
        {
            return _panInteraction.RespondToMouseMove(sender, e);
        }


        bool input = (bool)_inputInfo.GetValue(this);
        IGH_Attributes iGH_Attributes = document.FindAttributeByGrip(e.CanvasLocation, bLimitToOutside: false, input, !input, 20);

        iGH_Attributes ??= GH_AdvancedWireInteraction.GetRightAttribute(document, e, !input);

        if (iGH_Attributes == null)
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

        if (!input)
        {
            _pointInfo.SetValue(this, iGH_Attributes.OutputGrip);
        }
        else
        {
            _pointInfo.SetValue(this, iGH_Attributes.InputGrip);
        }



        Instances.CursorServer.AttachCursor(base.Canvas, "GH_Rewire");
        base.Canvas.Refresh();
        return GH_ObjectResponse.Handled;
    }

    public override GH_ObjectResponse RespondToMouseUp(GH_Canvas sender, GH_CanvasMouseEvent e)
    {

        Instances.CursorServer.ResetCursor(sender);

        if (!base.Canvas.IsDocument)
        {
            return GH_ObjectResponse.Release;
        }

        bool inTime = _isFirstUp && _timer.ElapsedMilliseconds < QuickConnectionAssemblyLoad.QuickConnectionMaxWaitTime;
        _isFirstUp = false;

        GH_Document document = base.Canvas.Document;
        IGH_Param target = ((IGH_Param)_targetInfo.GetValue(this));
        IGH_Param source = ((IGH_Param)_sourceInfo.GetValue(this));


        if (e.Button != MouseButtons.Left)
        {
            if (_panInteraction != null && GH_AdvancedWireInteraction.DistanceTo(e.ControlLocation, _panControlLocation) < 10)
            {
                Instances.ActiveCanvas.ActiveInteraction = null;
            }

            _panInteraction = null;
            return GH_ObjectResponse.Ignore;
        }
        else  if (target != null)
        {
            if (target != source)
            {

                base.Canvas.Document.AutoSave(GH_AutoSaveTrigger.wire_event);
                if ((bool)_inputInfo.GetValue(this))
                {
                    List<IGH_UndoAction> list =
                    [
                        .. document.UndoUtil.CreateWireEvent("rewire_source", source).Actions,
                        .. document.UndoUtil.CreateWireEvent("rewire_target", target).Actions,
                    ];
                    document.UndoUtil.RecordEvent("Rewire", list);
                    foreach (IGH_Param sorc in source.Sources)
                    {
                        target.AddSource(sorc);
                    }
                    source.RemoveAllSources();
                }
                else
                {
                    List<IGH_UndoAction> list2 = [];
                    foreach (IGH_Param param in (List<IGH_Param>)_paramInfo.GetValue(this))
                    {
                        list2.AddRange(document.UndoUtil.CreateWireEvent("rewire_source", param).Actions);
                        param.ReplaceSource(source, target);
                    }
                    document.UndoUtil.RecordEvent("Rewire", list2);
                }
            }

            sender.Document.NewSolution(expireAllObjects: false);
            GH_AdvancedWireInteraction._click = e.CanvasLocation;
            return GH_ObjectResponse.Release;
        }

        //Make Click To Enable Interaction.
        else if (inTime || GH_AdvancedWireInteraction.DistanceTo(e.CanvasLocation, CanvasPointDown) < 10)
        {
            return GH_ObjectResponse.Ignore;
        }
        return GH_ObjectResponse.Release;
    }


    public override GH_ObjectResponse RespondToKeyUp(GH_Canvas sender, KeyEventArgs e)
    {
        return GH_ObjectResponse.Ignore;
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

    public bool get_IsValid()
    {
        IGH_Param param = (IGH_Param)_sourceInfo.GetValue(this);
        if (param.GetType().FullName.Contains("Grasshopper.Kernel.Components.GH_PlaceholderParameter")
            && !param.Attributes.IsTopLevel && param.Attributes.GetTopLevel.DocObject is GH_Component com)
        {
            bool input = com.Params.Input.Contains(param);
            _inputInfo.SetValue(this, input);

            List<IGH_Param> parameters = [];
            List<PointF> grips = [];
            if (!input)
            {
                foreach (IGH_Param recipient in param.Recipients)
                {
                    parameters.Add(recipient);
                    grips.Add(recipient.Attributes.InputGrip);
                }
            }
            else
            {
                foreach (IGH_Param source in param.Sources)
                {
                    parameters.Add(source);
                    grips.Add(source.Attributes.OutputGrip);
                }
            }
            _paramsInfo.SetValue(this, parameters);
            _GripInfo.SetValue(this, grips);
        }
        return ((List<IGH_Param>)_paramInfo.GetValue(this)).Count > 0;
    }
}
