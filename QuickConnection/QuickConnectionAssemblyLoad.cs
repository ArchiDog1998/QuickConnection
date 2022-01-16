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
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
using System.Windows.Forms;

namespace QuickConnection
{
    public class QuickConnectionAssemblyLoad : GH_AssemblyPriority
    {
        private static readonly string _location = Path.Combine(Folders.SettingsFolder, "quickwires.json");


        internal static CreateObjectItems StaticCreateObjectItems = new CreateObjectItems();

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

        #region Json Edit
        internal static void SaveToJson()
        {
            JavaScriptSerializer ser = new JavaScriptSerializer() { MaxJsonLength = int.MaxValue};
            try
            {
                File.WriteAllText(_location, ser.Serialize(new CreateObjectItemsSave(StaticCreateObjectItems)));
            }
            catch(Exception ex)
            {
                MessageBox.Show(ex.Message, "Json Library Save Failed");
            }
        }

        internal static void RemoveAllQuickwireSettings()
        {
            if (File.Exists(_location))
                File.Delete(_location);
            StaticCreateObjectItems = new CreateObjectItems();
        }

        internal static void ResetToDefaultQuiceWIreSettings(bool isCoreOnly)
        {
            StaticCreateObjectItems.CreateDefaultStyle(isCoreOnly);
            SaveToJson();
        }
        #endregion
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
                if (File.Exists(_location))
                {
                    string jsonStr = File.ReadAllText(_location);
                    JavaScriptSerializer ser = new JavaScriptSerializer() { MaxJsonLength = int.MaxValue };
                    try
                    {
                        StaticCreateObjectItems = new CreateObjectItems(ser.Deserialize<CreateObjectItemsSave>(jsonStr));
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show(ex.Message, "Json Library Load Failed");
                    }
                }
                else
                {
                    StaticCreateObjectItems.CreateDefaultStyle(true);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }

            //Binding to respond.
            Instances.ActiveCanvas.MouseDown += ActiveCanvas_MouseDown;

            ToolStrip _canvasToolbar = editor.Controls[0].Controls[1] as ToolStrip;

            ToolStripSeparator toolStripSeparator = new ToolStripSeparator();
            toolStripSeparator.Margin = new Padding(2, 0, 2, 0);
            toolStripSeparator.Size = new Size(6, 40);
            _canvasToolbar.Items.Add(toolStripSeparator);

            ToolStripButton button = new ToolStripButton(Properties.Resources.QuickwireIcon_24) { Checked = UseQuickConnection, ToolTipText = "Use Quick Connection" };
            ToolStripMenuItem major = new ToolStripMenuItem("Quick Connection", Properties.Resources.QuickwireIcon_24) { Checked = UseQuickConnection};

            button.Click += (sender, e) =>
            {
                UseQuickConnection = button.Checked = major.Checked = !button.Checked;
            };
            major.Click += (sender, e) =>
            {
                UseQuickConnection = button.Checked = major.Checked = !major.Checked;
            };

            #region Add three function for set the default library.
            major.DropDownItems.Add(new ToolStripMenuItem("Set Core Only Library", null, (sender, e) => new Thread(() => 
            { 
                StaticCreateObjectItems.CreateDefaultStyle(true);
                SaveToJson();
            }).Start()) { ToolTipText = "Click to set the default quick connection library about all core document objects."});

            major.DropDownItems.Add(new ToolStripMenuItem("Set All Component's Library", null, (sender, e) => new Thread(() =>
            {
                StaticCreateObjectItems.CreateDefaultStyle(false);
                SaveToJson();
            }).Start()) { ToolTipText = "Click to set the default quick connection library about all document objects." });

            major.DropDownItems.Add(new ToolStripMenuItem("Clear Library", null, (sender, e) => 
            { 
                StaticCreateObjectItems = new CreateObjectItems();
                SaveToJson();
            })
            { ToolTipText = "Clear the Library." });
            #endregion

            _canvasToolbar.Items.Add(button);
            ((ToolStripMenuItem)editor.MainMenuStrip.Items[3]).DropDownItems.Insert(3, major);
        }

        private static void ActiveCanvas_MouseDown(object sender, MouseEventArgs e)
        {
            if (UseQuickConnection && e.Button == MouseButtons.Left)
            {
                IGH_MouseInteraction activeInteraction = Instances.ActiveCanvas.ActiveInteraction;
                if (activeInteraction != null && activeInteraction is GH_WireInteraction)
                {
                    Instances.ActiveCanvas.ActiveInteraction = new GH_AdvancedWireInteraction(activeInteraction.Owner,
                        new GH_CanvasMouseEvent(activeInteraction.Owner.Viewport, e), (IGH_Param)GH_AdvancedWireInteraction._sourceInfo.GetValue(activeInteraction));
                }
            }
        }
    }
}