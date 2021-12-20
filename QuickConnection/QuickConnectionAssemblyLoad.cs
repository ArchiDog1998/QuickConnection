using Grasshopper;
using Grasshopper.GUI;
using Grasshopper.GUI.Canvas;
using Grasshopper.GUI.Canvas.Interaction;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Components;
using Grasshopper.Kernel.Parameters;
using Grasshopper.Kernel.Parameters.Hints;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
using System.Windows.Forms;

namespace QuickConnection
{
    public class QuickConnectionAssemblyLoad : GH_AssemblyPriority
    {



        private static readonly string _location = Path.Combine(Folders.SettingsFolder, "quickwires.json");

        private static GH_WireInteraction _wire = null;
        private static readonly FieldInfo _sourceInfo = typeof(GH_WireInteraction).GetRuntimeFields().Where(m => m.Name.Contains("m_source")).First();
        private static readonly FieldInfo _targetInfo = typeof(GH_WireInteraction).GetRuntimeFields().Where(m => m.Name.Contains("m_target")).First();
        private static readonly FieldInfo _fromInputInfo = typeof(GH_WireInteraction).GetRuntimeFields().Where(m => m.Name.Contains("m_dragfrominput")).First();
        private static readonly MethodInfo _paintOverlay = typeof(GH_WireInteraction).GetRuntimeMethods().Where(m => m.Name.Contains("Canvas_DrawOverlay")).First();

        internal static CreateObjectItems StaticCreateObjectItems;

        public static bool UseQuickConnection
        {
            get => Instances.Settings.GetValue(nameof(UseQuickConnection), true);
            set => Instances.Settings.SetValue(nameof(UseQuickConnection), value);
        }

        public static double QuickConnectionWindowWidth
        {
            get => Instances.Settings.GetValue(nameof(QuickConnectionWindowWidth), 200.0);
            set => Instances.Settings.SetValue(nameof(QuickConnectionWindowWidth), value);
        }

        public static double QuickConnectionWindowHeight
        {
            get => Instances.Settings.GetValue(nameof(QuickConnectionWindowHeight), 200.0);
            set => Instances.Settings.SetValue(nameof(QuickConnectionWindowHeight), value);
        }

        internal static void SaveToJson()
        {
            JavaScriptSerializer ser = new JavaScriptSerializer();
            File.WriteAllText(_location, ser.Serialize(new CreateObjectItemsSave(StaticCreateObjectItems)));
        }

        internal static void RemoveAllQuickwireSettings()
        {
            if (File.Exists(_location))
                File.Delete(_location);
            StaticCreateObjectItems = new CreateObjectItems();
        }

        public override GH_LoadingInstruction PriorityLoad()
        {
            Instances.CanvasCreated += Instances_CanvasCreated;
            return GH_LoadingInstruction.Proceed;
        }

        private void Instances_CanvasCreated(GH_Canvas canvas)
        {
            Instances.CanvasCreated -= Instances_CanvasCreated;

            GH_DocumentEditor editor = Instances.DocumentEditor;
            if (editor == null)
            {
                Instances.ActiveCanvas.DocumentChanged += ActiveCanvas_DocumentChanged;
                return;
            }
            DoingSomethingFirst(editor);
        }

        private void ActiveCanvas_DocumentChanged(GH_Canvas sender, GH_CanvasDocumentChangedEventArgs e)
        {
            Instances.ActiveCanvas.DocumentChanged -= ActiveCanvas_DocumentChanged;

            GH_DocumentEditor editor = Instances.DocumentEditor;
            if (editor == null)
            {
                MessageBox.Show("Failed to find the menu.");
                return;
            }
            DoingSomethingFirst(editor);
        }

        private void DoingSomethingFirst(GH_DocumentEditor editor)
        {
            //Read from json.
            try
            {
                string jsonStr;
                if (File.Exists(_location))
                {
                    jsonStr = File.ReadAllText(_location);
                }
                else
                {
                    jsonStr = Properties.Resources.quickwires;
                }
                JavaScriptSerializer ser = new JavaScriptSerializer();
                StaticCreateObjectItems = new CreateObjectItems(ser.Deserialize<CreateObjectItemsSave>(jsonStr));
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }

            //Binding to respond.
            Instances.ActiveCanvas.MouseDown += ActiveCanvas_MouseDown;
            Instances.ActiveCanvas.MouseUp += ActiveCanvas_MouseUp;

            ToolStrip _canvasToolbar = editor.Controls[0].Controls[1] as ToolStrip;

            ToolStripSeparator toolStripSeparator = new ToolStripSeparator();
            toolStripSeparator.Margin = new Padding(2, 0, 2, 0);
            toolStripSeparator.Size = new Size(6, 40);
            _canvasToolbar.Items.Add(toolStripSeparator);

            ToolStripButton button = new ToolStripButton(Properties.Resources.QuickwireIcon_24) { Checked = UseQuickConnection, ToolTipText = "Use Quick Connection" };
            button.Click += (sender, e) =>
            {
                UseQuickConnection = button.Checked = !button.Checked;
            };
            _canvasToolbar.Items.Add(button);
        }


        #region QuickConnection Respound

        private static void ActiveCanvas_MouseDown(object sender, MouseEventArgs e)
        {
            if (_wire != null) return;
            if (UseQuickConnection && e.Button == MouseButtons.Left)
            {
                IGH_MouseInteraction activeInteraction = Instances.ActiveCanvas.ActiveInteraction;
                if (activeInteraction != null && activeInteraction is GH_WireInteraction)
                {
                    _wire = (GH_WireInteraction)activeInteraction;
                }
            }
        }

        private static void ActiveCanvas_MouseUp(object sender, MouseEventArgs e)
        {
            if (_wire == null) return;
            PointF mousePointCanvas = Instances.ActiveCanvas.Viewport.UnprojectPoint(e.Location);
            IGH_Param source = (IGH_Param)_sourceInfo.GetValue(_wire);

            if (UseQuickConnection && e.Button == MouseButtons.Left && _targetInfo.GetValue(_wire) == null && !source.Attributes.GetTopLevel.Bounds.Contains(mousePointCanvas)
                && DistanceTo(mousePointCanvas, _wire.CanvasPointDown) > 20)
            {
                Instances.ActiveCanvas.CanvasPostPaintOverlay += ActiveCanvas_CanvasPostPaintOverlay;
                Instances.ActiveCanvas.Refresh();



                Point clickLocation = Instances.ActiveCanvas.PointToScreen(e.Location);

                ChooseWindow choose = new ChooseWindow(source, (bool)_fromInputInfo.GetValue(_wire), mousePointCanvas);
                choose.WindowStartupLocation = System.Windows.WindowStartupLocation.Manual;
                choose.Left = clickLocation.X;
                choose.Top = clickLocation.Y;
                choose.Width = QuickConnectionWindowWidth;
                choose.Height = QuickConnectionWindowHeight;
                choose.Show();
            }
            else
            {
                _wire = null;
            }
        }

        public static void CloseWireEvent()
        {
            Instances.ActiveCanvas.CanvasPostPaintOverlay -= ActiveCanvas_CanvasPostPaintOverlay;
            _wire = null;
            Instances.ActiveCanvas.Refresh();
        }

        private static float DistanceTo(PointF a, PointF b)
        {
            return (float)Math.Sqrt(Math.Pow(a.X - b.X, 2) + Math.Pow(a.Y - b.Y, 2));
        }

        private static void ActiveCanvas_CanvasPostPaintOverlay(GH_Canvas sender)
        {
            if (_wire != null)
                _paintOverlay.Invoke(_wire, new object[] { Instances.ActiveCanvas });
        }

        internal static ToolStripDropDownMenu RespondToQuickWire(IGH_Param param, Guid guid, bool isInput, PointF pivot, bool isFirst = true)
        {
            SortedList<Guid, CreateObjectItem[]> dict = new SortedList<Guid, CreateObjectItem[]>();
            if (isInput)
            {
                dict = StaticCreateObjectItems.InputItems;
            }
            else
            {
                dict = StaticCreateObjectItems.OutputItems;
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

            CreateObjectItem[] items = new CreateObjectItem[0];
            if (dict.ContainsKey(guid))
            {
                items = dict[guid];
            }

            ToolStripDropDownMenu menu = new ToolStripDropDownMenu() { MaximumSize = new Size(500, 700) };

            if (isFirst)
            {

                if (param.VolatileDataCount > 1 && !isInput)
                {
                    IGH_ObjectProxy proxy = Instances.ComponentServer.EmitObjectProxy(new Guid("59daf374-bc21-4a5e-8282-5504fb7ae9ae"));

                    ToolStripMenuItem listItem = GH_DocumentObject.Menu_AppendItem(menu, "List");
                    listItem.Image = proxy.Icon;
                    CreateQuickWireMenu(listItem.DropDown, StaticCreateObjectItems.ListItems, param, pivot, (sender, e) =>
                    {
                        ToolStripMenuItem toolStripMenuItem = sender as ToolStripMenuItem;

                        if (toolStripMenuItem != null && toolStripMenuItem.Tag != null && toolStripMenuItem.Tag is CreateObjectItem[])
                        {
                            ObservableCollection<CreateObjectItem> structureLists = new ObservableCollection<CreateObjectItem>((CreateObjectItem[])toolStripMenuItem.Tag);
                            new QuickConnectionEditor(isInput, proxy.Icon, "List", structureLists, (par) => par.Access == GH_ParamAccess.list && par is Param_GenericObject,
                                (arr, isIn) =>
                                {
                                    StaticCreateObjectItems.ListItems = arr;

                                }).Show();
                        }
                    });

                    if (param.VolatileData.PathCount > 1)
                    {
                        GH_GraftTreeComponent tree = new GH_GraftTreeComponent();
                        ToolStripMenuItem treeItem = GH_DocumentObject.Menu_AppendItem(menu, "Tree");
                        treeItem.Image = tree.Icon_24x24;
                        CreateQuickWireMenu(treeItem.DropDown, StaticCreateObjectItems.TreeItems, param, pivot, (sender, e) =>
                        {
                            ToolStripMenuItem toolStripMenuItem = sender as ToolStripMenuItem;

                            if (toolStripMenuItem != null && toolStripMenuItem.Tag != null && toolStripMenuItem.Tag is CreateObjectItem[])
                            {
                                ObservableCollection<CreateObjectItem> structureLists = new ObservableCollection<CreateObjectItem>((CreateObjectItem[])toolStripMenuItem.Tag);
                                new QuickConnectionEditor(isInput, tree.Icon_24x24, "Tree", structureLists, (par) => par.Access == GH_ParamAccess.tree && par is Param_GenericObject,
                                    (arr, isIn) =>
                                    {
                                        StaticCreateObjectItems.TreeItems = arr;

                                    }).Show();
                            }
                        });
                    }
                }


                //Curve
                if (guid == new Param_Rectangle().ComponentGuid || guid == new Param_Circle().ComponentGuid || guid == new Param_Arc().ComponentGuid
                    || guid == new Param_Line().ComponentGuid)
                {
                    Param_Curve curve = new Param_Curve();
                    ToolStripMenuItem item = GH_DocumentObject.Menu_AppendItem(menu, curve.Name);
                    item.Image = curve.Icon_24x24;
                    item.DropDown = RespondToQuickWire(param, curve.ComponentGuid, isInput, pivot, false);
                }

                //Brep
                if (guid == new Param_Surface().ComponentGuid || guid == new Guid("{89CD1A12-0007-4581-99BA-66578665E610}"))
                {
                    Param_Brep brep = new Param_Brep();
                    ToolStripMenuItem item = GH_DocumentObject.Menu_AppendItem(menu, brep.Name);
                    item.Image = brep.Icon_24x24;
                    item.DropDown = RespondToQuickWire(param, brep.ComponentGuid, isInput, pivot, false);
                }

                //Geometry
                if (guid == new Param_Rectangle().ComponentGuid || guid == new Param_Circle().ComponentGuid || guid == new Param_Arc().ComponentGuid || guid == new Param_Line().ComponentGuid
                    || guid == new Param_Point().ComponentGuid || guid == new Param_Plane().ComponentGuid || guid == new Param_Vector().ComponentGuid
                    || guid == new Param_Curve().ComponentGuid || guid == new Param_Surface().ComponentGuid || guid == new Param_Brep().ComponentGuid || guid == new Param_Group().ComponentGuid
                    || guid == new Param_Mesh().ComponentGuid || guid == new Guid("{89CD1A12-0007-4581-99BA-66578665E610}") || guid == new Param_Box().ComponentGuid)
                {
                    Param_Geometry geo = new Param_Geometry();
                    ToolStripMenuItem item = GH_DocumentObject.Menu_AppendItem(menu, geo.Name);
                    item.Image = geo.Icon_24x24;
                    item.DropDown = RespondToQuickWire(param, geo.ComponentGuid, isInput, pivot, false);
                }

                //General
                if (guid != new Param_GenericObject().ComponentGuid)
                {
                    Param_GenericObject general = new Param_GenericObject();
                    ToolStripMenuItem item = GH_DocumentObject.Menu_AppendItem(menu, general.Name);
                    item.Image = general.Icon_24x24;
                    item.DropDown = RespondToQuickWire(param, general.ComponentGuid, isInput, pivot, false);
                }
                GH_DocumentObject.Menu_AppendSeparator(menu);
            }

            CreateQuickWireMenu(menu, items, param, pivot, (sender, e) => Menu_EditItemClicked(sender, guid, param, isInput));
            menu.Closed += (sender, e) =>
            {
                if (e.CloseReason == ToolStripDropDownCloseReason.AppFocusChange) return;
                Instances.ActiveCanvas.CanvasPostPaintOverlay -= ActiveCanvas_CanvasPostPaintOverlay;
                _wire = null;
                Instances.ActiveCanvas.Refresh();
            };

            return menu;
        }

        private static void CreateQuickWireMenu(ToolStrip menu, CreateObjectItem[] items, IGH_Param param, PointF pivot, EventHandler click)
        {
            foreach (CreateObjectItem createItem in items)
            {
                ToolStripMenuItem item = GH_DocumentObject.Menu_AppendItem(menu, createItem.ShowName, (sender, e) => Menu_CreateItemClicked(sender, param, pivot), createItem.Icon);
                item.Tag = createItem;
                if (!string.IsNullOrEmpty(createItem.InitString))
                {
                    item.ToolTipText = $"Init String:\n{createItem.InitString}";
                }
                else
                {
                    item.ToolTipText = "No Init String.";
                }
            }
            ToolStripMenuItem editItem = GH_DocumentObject.Menu_AppendItem(menu, "Edit", click);
            editItem.Image = Properties.Resources.EditIcon_24;
            editItem.Tag = items;
            editItem.ForeColor = Color.DimGray;
        }

        private static void Menu_CreateItemClicked(object sender, IGH_Param param, PointF pivot)
        {
            ToolStripMenuItem toolStripMenuItem = sender as ToolStripMenuItem;
            if (toolStripMenuItem != null && toolStripMenuItem.Tag != null && toolStripMenuItem.Tag is CreateObjectItem)
            {
                CreateObjectItem createItem = (CreateObjectItem)toolStripMenuItem.Tag;
                createItem.CreateObject(param, pivot);
                return;
            }
            MessageBox.Show("Something wrong with create object.");
        }


        private static void Menu_EditItemClicked(object sender, Guid guid, IGH_Param param, bool isInput)
        {
            ToolStripMenuItem toolStripMenuItem = sender as ToolStripMenuItem;

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

            if (toolStripMenuItem != null && toolStripMenuItem.Tag != null && toolStripMenuItem.Tag is CreateObjectItem[])
            {
                ObservableCollection<CreateObjectItem> structureLists = new ObservableCollection<CreateObjectItem>((CreateObjectItem[])toolStripMenuItem.Tag);
                new QuickConnectionEditor(isInput, icon, name, structureLists, (par) => par.ComponentGuid == guid,
                    (arr, isIn) =>
                    {
                        if (isIn)
                            StaticCreateObjectItems.InputItems[guid] = arr;
                        else
                            StaticCreateObjectItems.OutputItems[guid] = arr;

                    }).Show();
            }
        }

        #endregion

        ///// <summary>
        ///// Create Menu
        ///// </summary>
        ///// <returns></returns>
        //private static ToolStripMenuItem CreateQuickWireItem()
        //{
        //    ToolStripMenuItem major = CreateCheckBox("Quick Wire", Datas.UseQuickWire, Properties.Resources.QuickwireIcon_24, (boolean) => Datas.UseQuickWire = boolean);
        //    major.ToolTipText = "You can left click the component's param or double click floating param to choose which activeobjec you want to add.";

        //    ToolStripMenuItem click = new ToolStripMenuItem("Clear all quickwire settings");
        //    click.Click += (sender, e) =>
        //    {
        //        if (MessageBox.Show("Are you sure to remove all quickwire settings, and delete the quickwires.json file?", "Remove All?", MessageBoxButtons.OKCancel) == DialogResult.OK)
        //        {
        //            RemoveAllQuickwireSettings();
        //            MessageBox.Show("Succeed!");
        //        }
        //    };

        //    major.DropDownItems.Add(click);

        //    return major;
        //}
    }
}
