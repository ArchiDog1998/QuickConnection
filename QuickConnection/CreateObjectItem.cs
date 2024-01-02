using Grasshopper.GUI.Canvas;
using Grasshopper.Kernel;
using System;
using System.Drawing;
using System.Linq;
using System.Reflection;

namespace QuickConnection;

public class CreateObjectItem : IComparable<CreateObjectItem>
{
    public readonly static MethodInfo functions = typeof(GH_Canvas).GetRuntimeMethods().Where(m => m.Name.Contains("InstantiateNewObject") && !m.IsPublic).First();

    public ushort Index { get; }
    public Guid ObjectGuid { get; }
    public string InitString { get; set; }
    public Bitmap Icon { get; } = null;
    public string ShowName { get; } = "Not Found";
    public string Name { get; } = "Not Found";
    public bool IsInput { get; }

    private readonly bool isCoreLibrary = false;
    public CreateObjectItem(Guid guid, ushort index, string init, bool isInput)
    {
        ObjectGuid = guid;
        InitString = init;
        Index = index;
        IsInput = isInput;

        IGH_ObjectProxy proxy = Grasshopper.Instances.ComponentServer.EmitObjectProxy(guid);
        if(proxy == null) return;

        foreach (var assembly in Grasshopper.Instances.ComponentServer.Libraries)
        {
            if (proxy.LibraryGuid == assembly.Id)
            {
                isCoreLibrary = assembly.IsCoreLibrary;
                break;
            }
        }
        Icon = proxy.Icon;
        Name = proxy.Desc.Name;
        ShowName = $"{proxy.Desc.Name}[{index}]\n\nInitString: {init}\n\n" + proxy.Desc.Description;
    }

    public CreateObjectItem(CreateObjectItemSave save, bool isInput):this(save.ObjectGuid, save.Index, save.InitString, isInput)
    {

    }

    public IGH_DocumentObject CreateObject(IGH_Param param, PointF objCenter, Action<IGH_DocumentObject> action = null)
    {
        IGH_DocumentObject obj = Grasshopper.Instances.ComponentServer.EmitObject(ObjectGuid);
        if (obj == null) return null;

        action?.Invoke(obj);

        if (obj is IGH_Component)
        {
            IGH_Component com = obj as IGH_Component;
            AddAObjectToCanvas(obj, objCenter, InitString);

            if (IsInput)
            {
                param.AddSource(com.Params.Output[Index]);
            }
            else
            {
                com.Params.Input[Index].AddSource(param);
            }

            Grasshopper.Instances.ActiveCanvas.Document.NewSolution(false);
        }
        else if(obj is IGH_Param)
        {
            IGH_Param par = obj as IGH_Param;
            AddAObjectToCanvas(obj, objCenter, InitString);

            if (IsInput)
            {
                param.AddSource(par);
            }
            else
            {
                par.AddSource(param);
            }

            Grasshopper.Instances.ActiveCanvas.Document.NewSolution(false);
        }

        return obj;
    }

    public static void AddAObjectToCanvas(IGH_DocumentObject obj, PointF pivot, string init, bool update = false)
    {
        functions.Invoke(Grasshopper.Instances.ActiveCanvas, new object[] { obj, init, pivot, update });
    }

    public int CompareTo(CreateObjectItem other)
    {
        IGH_ObjectProxy thisProxy = Grasshopper.Instances.ComponentServer.EmitObjectProxy(this.ObjectGuid);
        IGH_ObjectProxy otherProxy = Grasshopper.Instances.ComponentServer.EmitObjectProxy(other.ObjectGuid);

        int compareRound1 = (other.isCoreLibrary).CompareTo(isCoreLibrary);

        if(compareRound1 != 0) return compareRound1;

        if (thisProxy.Desc.HasCategory && otherProxy.Desc.HasCategory)
        {
            int compareRound1_1 = thisProxy.Desc.Category.CompareTo(otherProxy.Desc.Category);
            if (compareRound1_1 != 0) return compareRound1_1;
        }

        if(thisProxy.Desc.HasSubCategory && otherProxy.Desc.HasSubCategory)
        {
            int compareRound2 = thisProxy.Desc.SubCategory.CompareTo(otherProxy.Desc.SubCategory);
            if (compareRound2 != 0) return compareRound2;
        }

        return thisProxy.Desc.Name.CompareTo(otherProxy.Desc.Name);
    }
}

public struct CreateObjectItemSave
{
    public Guid ObjectGuid { get; set; }
    public string InitString { get; set; }
    public ushort Index { get; set; }

    public CreateObjectItemSave(Guid guid, string init, ushort index)
    {
        ObjectGuid = guid;
        InitString = init;
        Index = index;
    }

    public CreateObjectItemSave(CreateObjectItem item)
    {
        ObjectGuid = item.ObjectGuid;
        InitString = item.InitString;
        Index = item.Index;
    }
}
