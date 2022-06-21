using Grasshopper.GUI;
using Grasshopper.GUI.Canvas;
using Grasshopper.GUI.Canvas.Interaction;
using Grasshopper.Kernel;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace QuickConnection
{
    internal class GH_AdvancedRewireInteraction : GH_RewireInteraction
    {
        private static readonly FieldInfo _paramInfo = typeof(GH_RewireInteraction).GetRuntimeFields().First(m => m.Name.Contains("m_params"));
        private static readonly FieldInfo _sourcesInfo = typeof(GH_RewireInteraction).GetRuntimeFields().First(m => m.Name.Contains("m_source"));
        private static readonly FieldInfo _inputInfo = typeof(GH_RewireInteraction).GetRuntimeFields().First(m => m.Name.Contains("m_input"));
        private static readonly FieldInfo _paramsInfo = typeof(GH_RewireInteraction).GetRuntimeFields().First(m => m.Name.Contains("m_params"));
        private static readonly FieldInfo _GripInfo = typeof(GH_RewireInteraction).GetRuntimeFields().First(m => m.Name.Contains("m_grips"));

        public GH_AdvancedRewireInteraction(GH_Canvas iParent, GH_CanvasMouseEvent mEvent, IGH_Param Source)
            :base(iParent, mEvent, Source)
        {

        }

        public bool get_IsValid()
        {
            IGH_Param param = (IGH_Param)_sourcesInfo.GetValue(this);
            if (param.GetType().FullName.Contains("Grasshopper.Kernel.Components.GH_PlaceholderParameter")
                && !param.Attributes.IsTopLevel && param.Attributes.GetTopLevel.DocObject is GH_Component com)
            {
                bool input = com.Params.Input.Contains(param);
                _inputInfo.SetValue(this, input);

                List<IGH_Param> parameters = new List<IGH_Param>();
                List<PointF> grips = new List<PointF>();
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
}
