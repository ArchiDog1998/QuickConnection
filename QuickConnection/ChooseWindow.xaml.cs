using Grasshopper;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Components;
using Grasshopper.Kernel.Parameters;
using Grasshopper.Kernel.Parameters.Hints;
using Grasshopper.Kernel.Special;
using Grasshopper.Kernel.Types;
using SimpleGrasshopper.Util;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Drawing;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using Image = System.Windows.Controls.Image;

namespace QuickConnection;

/// <summary>
/// Interaction logic for ChooseWindow.xaml
/// </summary>
public partial class ChooseWindow : Window
{
    private readonly IGH_Param _owner;
    private readonly PointF _position;

    private static void ChangeParamId(Guid hintid, ref Guid guid)
    {
        if (hintid == new GH_ArcHint().HintID)
            guid = new Param_Arc().ComponentGuid;
        else if (hintid == new GH_BooleanHint_CS().HintID || hintid == new GH_BooleanHint_VB().HintID)
            guid = new Param_Boolean().ComponentGuid;
        else if (hintid == new GH_BoxHint().HintID)
            guid = new Param_Box().ComponentGuid;
        else if (hintid == new GH_BrepHint().HintID)
            guid = new Param_Brep().ComponentGuid;
        else if (hintid == new GH_CircleHint().HintID)
            guid = new Param_Circle().ComponentGuid;
        else if (hintid == new GH_ColorHint().HintID)
            guid = new Param_Colour().ComponentGuid;
        else if (hintid == new GH_ComplexHint().HintID)
            guid = new Param_Complex().ComponentGuid;
        else if (hintid == new GH_CurveHint().HintID)
            guid = new Param_Curve().ComponentGuid;
        else if (hintid == new GH_DateTimeHint().HintID)
            guid = new Param_Time().ComponentGuid;
        else if (hintid == new GH_DoubleHint_CS().HintID || hintid == new GH_DoubleHint_VB().HintID)
            guid = new Param_Number().ComponentGuid;
        else if (hintid == new GH_GeometryBaseHint().HintID)
            guid = new Param_Geometry().ComponentGuid;
        else if (hintid == new GH_GuidHint().HintID)
            guid = new Param_Guid().ComponentGuid;
        else if (hintid == new GH_IntegerHint_CS().HintID || hintid == new GH_IntegerHint_VB().HintID)
            guid = new Param_Integer().ComponentGuid;
        else if (hintid == new GH_IntervalHint().HintID)
            guid = new Param_Interval().ComponentGuid;
        else if (hintid == new GH_LineHint().HintID)
            guid = new Param_Line().ComponentGuid;
        else if (hintid == new GH_MeshHint().HintID)
            guid = new Param_Mesh().ComponentGuid;
        else if (hintid == new GH_NullHint().HintID)
            guid = new Param_GenericObject().ComponentGuid;
        else if (hintid == new GH_PlaneHint().HintID)
            guid = new Param_Plane().ComponentGuid;
        else if (hintid == new GH_Point3dHint().HintID)
            guid = new Param_Point().ComponentGuid;
        else if (hintid == new GH_PolylineHint().HintID)
            guid = new Param_Curve().ComponentGuid;
        else if (hintid == new GH_Rectangle3dHint().HintID)
            guid = new Param_Rectangle().ComponentGuid;
        else if (hintid == new GH_StringHint_CS().HintID || hintid == new GH_StringHint_VB().HintID)
            guid = new Param_String().ComponentGuid;
        else if (hintid == new GH_SubDHint().HintID)
            guid = new Param_SubD().ComponentGuid;
        else if (hintid == new GH_SurfaceHint().HintID)
            guid = new Param_Surface().ComponentGuid;
        else if (hintid == new GH_TransformHint().HintID)
            guid = new Param_Transform().ComponentGuid;
        else if (hintid == new GH_UVIntervalHint().HintID)
            guid = new Param_Interval2D().ComponentGuid;
        else if (hintid == new GH_Vector3dHint().HintID)
            guid = new Param_Vector().ComponentGuid;
    }

    public ChooseWindow(IGH_Param param, bool isInput, PointF pivot)
    {
        _owner = param;
        _position = pivot;
        Guid guid = param.ComponentGuid;

        SortedList<Guid, CreateObjectItem[]> dict = [];
        if (isInput)
        {
            dict = SimpleAssemblyPriority.StaticCreateObjectItems.InputItems;
        }
        else
        {
            dict = SimpleAssemblyPriority.StaticCreateObjectItems.OutputItems;
        }

        //Change Guid.
        if (param is Param_ScriptVariable script)
        {
            ChangeParamId(script.TypeHint.HintID, ref guid);
        }
        else if(param.GetType().FullName == "RhinoCodePluginGH.Parameters.ScriptVariableParam")
        {
            var converter = param.GetType().GetAllRuntimeFields().First(f => f.Name == "_converter").GetValue(param);
            var mcId = converter.GetType().GetAllRuntimeProperties().First(p => p.Name == "Id").GetValue(converter);
            var id = mcId.GetType().GetAllRuntimeProperties().First(p => p.Name == "Id").GetValue(mcId);
            ChangeParamId((Guid)id, ref guid);
        }

        InitializeComponent();

        if (!dict.TryGetValue(guid, out CreateObjectItem[] objItems)) objItems = [];
        Image objImage = CreateHeader(param.Icon_24x24);
        ObjectTitle.MouseDoubleClick += (sender, e) =>
        {
            Menu_EditItemLeftClicked(param.ComponentGuid, isInput);
        };
        ObjectTitle.MouseRightButtonUp += (sender, e) =>
        {
            Menu_EditItemRightClicked(objItems, param.ComponentGuid, param, isInput);
        };
        ObjectTitle.Header = objImage;
        ObjectList.ItemsSource = objItems;

        //Tree List Tree Quick Connect
        if (!isInput)
        {
            if (param.VolatileDataCount > 1)
            {
                IGH_ObjectProxy proxy = Instances.ComponentServer.EmitObjectProxy(new Guid("59daf374-bc21-4a5e-8282-5504fb7ae9ae"));
                Image image = CreateHeader(proxy.Icon);

                ListTitle.MouseRightButtonUp += (sender, e) =>
                {
                    ObservableCollection<CreateObjectItem> structureLists = new ObservableCollection<CreateObjectItem>(SimpleAssemblyPriority.StaticCreateObjectItems.ListItems);

                    new QuickConnectionEditor(isInput, proxy.Icon, "List", structureLists, (par) => par.Access == GH_ParamAccess.list && par is Param_GenericObject,
                        (arr, isIn) =>
                        {
                            SimpleAssemblyPriority.StaticCreateObjectItems.ListItems = arr;

                        }).Show();
                };
                ListTitle.Header = image;
                ListList.ItemsSource = SimpleAssemblyPriority.StaticCreateObjectItems.ListItems;

            }
            else ListTitle.Visibility = Visibility.Collapsed;

            if(param.VolatileData.PathCount > 1)
            {
                GH_GraftTreeComponent tree = new GH_GraftTreeComponent();
                Image image = CreateHeader(tree.Icon_24x24);
                TreeTitle.MouseRightButtonUp += (sender, e) =>
                {
                    ObservableCollection<CreateObjectItem> structureLists = new ObservableCollection<CreateObjectItem>(SimpleAssemblyPriority.StaticCreateObjectItems.TreeItems);
                    new QuickConnectionEditor(isInput, tree.Icon_24x24, "Tree", structureLists, (par) => par.Access == GH_ParamAccess.tree && par is Param_GenericObject,
                    (arr, isIn) =>
                    {
                        SimpleAssemblyPriority.StaticCreateObjectItems.TreeItems = arr;

                    }).Show();
                };
                TreeTitle.Header = image;
                TreeList.ItemsSource = SimpleAssemblyPriority.StaticCreateObjectItems.TreeItems;
            }
            else TreeTitle.Visibility = Visibility.Collapsed;
        } else
        {
            ListTitle.Visibility = Visibility.Collapsed;
            TreeTitle.Visibility = Visibility.Collapsed;
        }

        //Curve
        if (guid == new Param_Rectangle().ComponentGuid || guid == new Param_Circle().ComponentGuid || guid == new Param_Arc().ComponentGuid
            || guid == new Param_Line().ComponentGuid)
        {
            Param_Curve par = new Param_Curve();
            Image image = CreateHeader(par.Icon_24x24);
            if (!dict.TryGetValue(par.ComponentGuid, out CreateObjectItem[] items)) items = [];
            CurveTitle.MouseDoubleClick += (sender, e) =>
            {
                Menu_EditItemLeftClicked(param.ComponentGuid, isInput);
            };
            CurveTitle.MouseRightButtonUp += (sender, e) =>
            {
                Menu_EditItemRightClicked(items, par.ComponentGuid, param, isInput);
            };
            CurveTitle.Header = image;
            CurveList.ItemsSource = items;
        }
        else CurveTitle.Visibility = Visibility.Collapsed;

        //Brep
        if (guid == new Param_Surface().ComponentGuid || guid == new Guid("{89CD1A12-0007-4581-99BA-66578665E610}"))
        {
            Param_Brep par = new Param_Brep();
            Image image = CreateHeader(par.Icon_24x24);
            if (!dict.TryGetValue(par.ComponentGuid, out CreateObjectItem[] items)) items = [];
            BrepTitle.MouseDoubleClick += (sender, e) =>
            {
                Menu_EditItemLeftClicked(param.ComponentGuid, isInput);
            };
            BrepTitle.MouseRightButtonUp += (sender, e) =>
            {
                Menu_EditItemRightClicked(items, par.ComponentGuid, param, isInput);
            };
            BrepTitle.Header = image;
            BrepList.ItemsSource = items;
        }
        else BrepTitle.Visibility = Visibility.Collapsed;

        //Geometry
        if (guid == new Param_Rectangle().ComponentGuid || guid == new Param_Circle().ComponentGuid || guid == new Param_Arc().ComponentGuid || guid == new Param_Line().ComponentGuid
            || guid == new Param_Point().ComponentGuid || guid == new Param_Plane().ComponentGuid || guid == new Param_Vector().ComponentGuid
            || guid == new Param_Curve().ComponentGuid || guid == new Param_Surface().ComponentGuid || guid == new Param_Brep().ComponentGuid || guid == new Param_Group().ComponentGuid
            || guid == new Param_Mesh().ComponentGuid || guid == new Param_SubD().ComponentGuid || guid == new Param_Box().ComponentGuid 
            || guid == new GH_GeometryCache().ComponentGuid)
        {
            Param_Geometry par = new Param_Geometry();
            Image image = CreateHeader(par.Icon_24x24);
            if (!dict.TryGetValue(par.ComponentGuid, out CreateObjectItem[] items)) items = [];
            GeoTitle.MouseDoubleClick += (sender, e) =>
            {
                Menu_EditItemLeftClicked(param.ComponentGuid, isInput);
            };
            GeoTitle.MouseRightButtonUp += (sender, e) =>
            {
                Menu_EditItemRightClicked(items, par.ComponentGuid, param, isInput);
            };
            GeoTitle.Header = image;
            GeoList.ItemsSource = items;
        }
        else GeoTitle.Visibility = Visibility.Collapsed;

        //General
        if (guid != new Param_GenericObject().ComponentGuid)
        {
            Param_GenericObject par = new Param_GenericObject();
            Image image = CreateHeader(par.Icon_24x24);
            if (!dict.TryGetValue(par.ComponentGuid, out CreateObjectItem[] items)) items = [];
            GeneralTitle.MouseDoubleClick += (sender, e) =>
            {
                Menu_EditItemLeftClicked(param.ComponentGuid, isInput);
            };
            GeneralTitle.MouseRightButtonUp += (sender, e) =>
            {
                Menu_EditItemRightClicked(items, par.ComponentGuid, param, isInput);
            };
            GeneralTitle.Header = image;
            GeneralList.ItemsSource = items;
        }
        else GeneralTitle.Visibility = Visibility.Collapsed;
    }
    private Image CreateHeader(Bitmap icon)
    {
        Image image = new Image() { Source = BitmapConverter.ToImageSource(icon), Width = 16, Height = 16, 
            ToolTip = "Drag to move window, Right Click to Edit, Double Click to the same persistent param as the icon if it is.", };
        image.MouseMove += (sender, e) =>
        {
            if (e.LeftButton == MouseButtonState.Pressed)
                this.DragMove();
        };
        return image;
    }

    private void Menu_EditItemLeftClicked(Guid guid, bool isInput)
    {
        IGH_DocumentObject obj = Instances.ComponentServer.EmitObject(guid);
        if (obj == null) return;
        new CreateObjectItem(guid, 0, "", isInput).CreateObject(_owner, _position);
        this.Close();
    }

    private static void Menu_EditItemRightClicked(CreateObjectItem[] items, Guid guid, IGH_Param param, bool isInput)
    {

        string name;
        Bitmap icon;
        IGH_ObjectProxy proxy = Instances.ComponentServer.EmitObjectProxy(guid);
        if (proxy == null)
        {
            name = param.Name;
            icon = param.Icon_24x24;
        }
        else
        {
            name = proxy.Desc.Name;
            icon = proxy.Icon;
        }

        ObservableCollection<CreateObjectItem> structureLists = new ObservableCollection<CreateObjectItem>(items);
        new QuickConnectionEditor(isInput, icon, name, structureLists, (par) => par.ComponentGuid == guid,
            (arr, isIn) =>
            {
                if (isIn)
                    SimpleAssemblyPriority.StaticCreateObjectItems.InputItems[guid] = arr;
                else
                    SimpleAssemblyPriority.StaticCreateObjectItems.OutputItems[guid] = arr;

            }).Show();
    }

    protected override void OnDeactivated(EventArgs e)
    {
        try
        {
            Instances.ActiveCanvas.ActiveInteraction = null;
            this.Close();
        }
        catch { }
    }

    protected override void OnKeyUp(KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            Instances.ActiveCanvas.ActiveInteraction = null;
            Close();
            return;
        }
        base.OnKeyUp(e);
    }

    protected override void OnClosed(EventArgs e)
    {
        SimpleAssemblyPriority.QuickConnectionWindowWidth = Width;
        SimpleAssemblyPriority.QuickConnectionWindowHeight = Height;
        GH_AdvancedWireInteraction._lastCanvasLoacation = PointF.Empty;
        base.OnClosed(e);
    }

    private void SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (sender is not ListBox listBox) return;

        CreateObjectItem cItem = (CreateObjectItem)listBox.SelectedItem;

        if (cItem == null) return;

        Instances.ActiveCanvas.ActiveInteraction = null;
        cItem.CreateObject(_owner, _position);
        this.Close();
    }
}
/// <summary>
/// New Line Object for Wrap Panel.
/// </summary>
public class NewLine : FrameworkElement
{
    public NewLine()
    {
        Height = 0;
        var binding = new Binding
        {
            RelativeSource = new RelativeSource(RelativeSourceMode.FindAncestor, typeof(WrapPanel), 1),
            Path = new PropertyPath("ActualWidth")
        };
        BindingOperations.SetBinding(this, WidthProperty, binding);
    }
}
