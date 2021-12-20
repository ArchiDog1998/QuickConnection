using Grasshopper;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Components;
using Grasshopper.Kernel.Parameters;
using Grasshopper.Kernel.Parameters.Hints;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using Image = System.Windows.Controls.Image;

namespace QuickConnection
{
    /// <summary>
    /// Interaction logic for ChooseWindow.xaml
    /// </summary>
    public partial class ChooseWindow : Window
    {
        private IGH_Param _owner;
        private PointF _position;

        public ChooseWindow(IGH_Param param, bool isInput, PointF pivot)
        {
            _owner = param;
            _position = pivot;
            Guid guid = param.ComponentGuid;

            SortedList<Guid, CreateObjectItem[]> dict = new SortedList<Guid, CreateObjectItem[]>();
            if (isInput)
            {
                dict = QuickConnectionAssemblyLoad.StaticCreateObjectItems.InputItems;
            }
            else
            {
                dict = QuickConnectionAssemblyLoad.StaticCreateObjectItems.OutputItems;
            }

            //Change Guid.
            if (param is Param_ScriptVariable && guid == new Param_ScriptVariable().ComponentGuid)
            {
                Param_ScriptVariable script = (Param_ScriptVariable)param;

                if (script.TypeHint != null)
                {
                    if (script.TypeHint is GH_ArcHint)
                        guid = new Param_Arc().ComponentGuid;
                    else if (script.TypeHint is GH_BooleanHint_CS || script.TypeHint is GH_BooleanHint_VB)
                        guid = new Param_Boolean().ComponentGuid;
                    else if (script.TypeHint is GH_BoxHint)
                        guid = new Param_Box().ComponentGuid;
                    else if (script.TypeHint is GH_BrepHint)
                        guid = new Param_Brep().ComponentGuid;
                    else if (script.TypeHint is GH_CircleHint)
                        guid = new Param_Circle().ComponentGuid;
                    else if (script.TypeHint is GH_ColorHint)
                        guid = new Param_Colour().ComponentGuid;
                    else if (script.TypeHint is GH_ComplexHint)
                        guid = new Param_Complex().ComponentGuid;
                    else if (script.TypeHint is GH_CurveHint)
                        guid = new Param_Curve().ComponentGuid;
                    else if (script.TypeHint is GH_DateTimeHint)
                        guid = new Param_Time().ComponentGuid;
                    else if (script.TypeHint is GH_DoubleHint_CS || script.TypeHint is GH_DoubleHint_VB)
                        guid = new Param_Number().ComponentGuid;
                    else if (script.TypeHint is GH_GeometryBaseHint)
                        guid = new Param_Geometry().ComponentGuid;
                    else if (script.TypeHint is GH_GuidHint)
                        guid = new Param_Guid().ComponentGuid;
                    else if (script.TypeHint is GH_IntegerHint_CS || script.TypeHint is GH_IntegerHint_VB)
                        guid = new Param_Integer().ComponentGuid;
                    else if (script.TypeHint is GH_IntervalHint)
                        guid = new Param_Interval().ComponentGuid;
                    else if (script.TypeHint is GH_LineHint)
                        guid = new Param_Line().ComponentGuid;
                    else if (script.TypeHint is GH_MeshHint)
                        guid = new Param_Mesh().ComponentGuid;
                    else if (script.TypeHint is GH_NullHint)
                        guid = new Param_GenericObject().ComponentGuid;
                    else if (script.TypeHint is GH_PlaneHint)
                        guid = new Param_Plane().ComponentGuid;
                    else if (script.TypeHint is GH_Point3dHint)
                        guid = new Param_Point().ComponentGuid;
                    else if (script.TypeHint is GH_PolylineHint)
                        guid = new Param_Curve().ComponentGuid;
                    else if (script.TypeHint is GH_Rectangle3dHint)
                        guid = new Param_Rectangle().ComponentGuid;
                    else if (script.TypeHint is GH_StringHint_CS || script.TypeHint is GH_StringHint_VB)
                        guid = new Param_String().ComponentGuid;
                    else if (script.TypeHint?.TypeName == "SubD")
                        guid = new Guid("{89CD1A12-0007-4581-99BA-66578665E610}");
                    else if (script.TypeHint is GH_SurfaceHint)
                        guid = new Param_Surface().ComponentGuid;
                    else if (script.TypeHint is GH_TransformHint)
                        guid = new Param_Transform().ComponentGuid;
                    else if (script.TypeHint is GH_UVIntervalHint)
                        guid = new Param_Interval2D().ComponentGuid;
                    else if (script.TypeHint is GH_Vector3dHint)
                        guid = new Param_Vector().ComponentGuid;
                }

            }

            InitializeComponent();

            if (!dict.TryGetValue(guid, out CreateObjectItem[] objItems)) objItems = new CreateObjectItem[0];
            Image objImage = CreateHeader(param.Icon_24x24);
            ObjectTitle.MouseRightButtonUp += (sender, e) =>
            {
                Menu_EditItemClicked(objItems, param.ComponentGuid, param, isInput);
            };
            ObjectTitle.Header = objImage;
            ObjectList.ItemsSource = objItems;

            //Tree List Quick Connect
            if (!isInput)
            {
                if (param.VolatileDataCount > 1)
                {
                    IGH_ObjectProxy proxy = Instances.ComponentServer.EmitObjectProxy(new Guid("59daf374-bc21-4a5e-8282-5504fb7ae9ae"));
                    Image image = CreateHeader(proxy.Icon);

                    ListTitle.MouseRightButtonUp += (sender, e) =>
                    {
                        ObservableCollection<CreateObjectItem> structureLists = new ObservableCollection<CreateObjectItem>(QuickConnectionAssemblyLoad.StaticCreateObjectItems.ListItems);

                        new QuickConnectionEditor(isInput, proxy.Icon, "List", structureLists, (par) => par.Access == GH_ParamAccess.list && par is Param_GenericObject,
                            (arr, isIn) =>
                            {
                                QuickConnectionAssemblyLoad.StaticCreateObjectItems.ListItems = arr;

                            }).Show();
                    };
                    ListTitle.Header = image;
                    ListList.ItemsSource = QuickConnectionAssemblyLoad.StaticCreateObjectItems.ListItems;

                }
                else ListTitle.Visibility = Visibility.Collapsed;

                if(param.VolatileData.PathCount > 1)
                {
                    GH_GraftTreeComponent tree = new GH_GraftTreeComponent();
                    Image image = CreateHeader(tree.Icon_24x24);
                    TreeTitle.MouseRightButtonUp += (sender, e) =>
                    {
                        ObservableCollection<CreateObjectItem> structureLists = new ObservableCollection<CreateObjectItem>(QuickConnectionAssemblyLoad.StaticCreateObjectItems.TreeItems);
                        new QuickConnectionEditor(isInput, tree.Icon_24x24, "Tree", structureLists, (par) => par.Access == GH_ParamAccess.tree && par is Param_GenericObject,
                        (arr, isIn) =>
                        {
                            QuickConnectionAssemblyLoad.StaticCreateObjectItems.TreeItems = arr;

                        }).Show();
                    };
                    TreeTitle.Header = image;
                    TreeList.ItemsSource = QuickConnectionAssemblyLoad.StaticCreateObjectItems.TreeItems;
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
                if (!dict.TryGetValue(par.ComponentGuid, out CreateObjectItem[] items)) items = new CreateObjectItem[0];
                CurveTitle.MouseRightButtonUp += (sender, e) =>
                {
                    Menu_EditItemClicked(items, par.ComponentGuid, param, isInput);
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
                if (!dict.TryGetValue(par.ComponentGuid, out CreateObjectItem[] items)) items = new CreateObjectItem[0];
                BrepTitle.MouseRightButtonUp += (sender, e) =>
                {
                    Menu_EditItemClicked(items, par.ComponentGuid, param, isInput);
                };
                BrepTitle.Header = image;
                BrepList.ItemsSource = items;
            }
            else BrepTitle.Visibility = Visibility.Collapsed;

            //Geometry
            if (guid == new Param_Rectangle().ComponentGuid || guid == new Param_Circle().ComponentGuid || guid == new Param_Arc().ComponentGuid || guid == new Param_Line().ComponentGuid
                || guid == new Param_Point().ComponentGuid || guid == new Param_Plane().ComponentGuid || guid == new Param_Vector().ComponentGuid
                || guid == new Param_Curve().ComponentGuid || guid == new Param_Surface().ComponentGuid || guid == new Param_Brep().ComponentGuid || guid == new Param_Group().ComponentGuid
                || guid == new Param_Mesh().ComponentGuid || guid == new Guid("{89CD1A12-0007-4581-99BA-66578665E610}") || guid == new Param_Box().ComponentGuid)
            {
                Param_Geometry par = new Param_Geometry();
                Image image = CreateHeader(par.Icon_24x24);
                if (!dict.TryGetValue(par.ComponentGuid, out CreateObjectItem[] items)) items = new CreateObjectItem[0];
                GeoTitle.MouseRightButtonUp += (sender, e) =>
                {
                    Menu_EditItemClicked(items, par.ComponentGuid, param, isInput);
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
                if (!dict.TryGetValue(par.ComponentGuid, out CreateObjectItem[] items)) items = new CreateObjectItem[0];
                GeneralTitle.MouseRightButtonUp += (sender, e) =>
                {
                    Menu_EditItemClicked(items, par.ComponentGuid, param, isInput);
                };
                GeneralTitle.Header = image;
                GeneralList.ItemsSource = items;
            }
            else GeneralTitle.Visibility = Visibility.Collapsed;
        }
        private Image CreateHeader(Bitmap icon)
        {
            Image image = new Image() { Source = BitmapConverter.ToImageSource(icon), Width = 16, Height = 16, ToolTip = "Right Click to Edit." };
            image.MouseMove += (sender, e) =>
            {
                if (e.LeftButton == MouseButtonState.Pressed)
                    this.DragMove();
            };
            return image;
        }

        private static void Menu_EditItemClicked(CreateObjectItem[] items, Guid guid, IGH_Param param, bool isInput)
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
                        QuickConnectionAssemblyLoad.StaticCreateObjectItems.InputItems[guid] = arr;
                    else
                        QuickConnectionAssemblyLoad.StaticCreateObjectItems.OutputItems[guid] = arr;

                }).Show();
        }

        protected override void OnDeactivated(EventArgs e)
        {
            try
            {
                QuickConnectionAssemblyLoad.CloseWireEvent();
                this.Close();
            }
            catch { }
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            if (e.Key == Key.Escape) 
            {
                QuickConnectionAssemblyLoad.CloseWireEvent();
                Close();
            }
        }

        protected override void OnClosed(EventArgs e)
        {
            QuickConnectionAssemblyLoad.QuickConnectionWindowWidth = Width;
            QuickConnectionAssemblyLoad.QuickConnectionWindowHeight = Height;
            base.OnClosed(e);
        }

        private void SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            ListBox listBox = sender as ListBox;
            if (listBox == null) return;

            CreateObjectItem cItem = (CreateObjectItem)listBox.SelectedItem;

            if (cItem == null) return;

            QuickConnectionAssemblyLoad.CloseWireEvent();
            cItem.CreateObject(_owner, _position);
            this.Close();
        }
    }
}
