using Grasshopper;
using Grasshopper.GUI;
using Grasshopper.GUI.Base;
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
using System.Runtime.CompilerServices;
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
        internal static readonly FieldInfo _sourceInfo = typeof(GH_RewireInteraction).GetRuntimeFields().Where(m => m.Name.Contains("m_source")).First();
        internal static readonly FieldInfo _inputInfo = typeof(GH_RewireInteraction).GetRuntimeFields().Where(m => m.Name.Contains("m_input")).First();


        internal static CreateObjectItems StaticCreateObjectItems = new CreateObjectItems();

        public static bool CantWireEasily
        {
            get => Instances.Settings.GetValue(nameof(CantWireEasily), false);
            set => Instances.Settings.SetValue(nameof(CantWireEasily), value);
        }

        public static bool UseQuickConnection
        {
            get => Instances.Settings.GetValue(nameof(UseQuickConnection), true);
            set => Instances.Settings.SetValue(nameof(UseQuickConnection), value);
        }

        public static bool UseFuzzyConnection
        {
            get => Instances.Settings.GetValue(nameof(UseFuzzyConnection), true);
            set => Instances.Settings.SetValue(nameof(UseFuzzyConnection), value);
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
        private static int _quickConnectionMaxWaitTimeDefault = 100;
        public static int QuickConnectionMaxWaitTime
        {
            get => Instances.Settings.GetValue(nameof(QuickConnectionMaxWaitTime), _quickConnectionMaxWaitTimeDefault);
            set => Instances.Settings.SetValue(nameof(QuickConnectionMaxWaitTime), value);
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

        private static void LoadFromLocal()
        {
            JavaScriptSerializer ser = new JavaScriptSerializer() { MaxJsonLength = int.MaxValue };
            StaticCreateObjectItems = new CreateObjectItems(ser.Deserialize<CreateObjectItemsSave>(Properties.Resources.quickwires));
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
                    LoadFromLocal();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }

            //Binding to respond.
            Instances.ActiveCanvas.MouseDown += ActiveCanvas_MouseDown;

            ExchangeMethod(
                typeof(GH_RewireInteraction).GetRuntimeMethods().Where(m => m.Name.Contains("get_IsValid")).First(),
                typeof(GH_AdvancedRewireInteraction).GetRuntimeMethods().Where(m => m.Name.Contains("get_IsValid")).First()
            );

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

            major.DropDownItems.Add(new ToolStripMenuItem("Use Fuzzy Connection", null, (sender, e) =>
            {
                ToolStripMenuItem item = (ToolStripMenuItem)sender;
                item.Checked = UseFuzzyConnection = !item.Checked;
            })
            { 
                Checked = UseFuzzyConnection,
            });

            GH_Component.Menu_AppendSeparator(major.DropDown);


            CreateNumberBox(major, "Click Wait Time", "The time from the first down to the first up of the mouse is less than this value, then it is considered a click.", 
                QuickConnectionMaxWaitTime, (v) => QuickConnectionMaxWaitTime = (int)v, _quickConnectionMaxWaitTimeDefault, 1000, 0);

            major.DropDownItems.Add(new ToolStripMenuItem("Can't Wire Easily", null, (sender, e) =>
            {
                ToolStripMenuItem item = (ToolStripMenuItem)sender;
                item.Checked = CantWireEasily = !item.Checked;
            })
            {
                ToolTipText = "If you can't wire functionally, please check it.",
            });


            GH_DocumentObject.Menu_AppendSeparator(major.DropDown);

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

            major.DropDownItems.Add(new ToolStripMenuItem("Load Default Library", null, (sender, e) => new Thread(() =>
            {
                LoadFromLocal();
                SaveToJson();
            }).Start())
            { ToolTipText = "Click to load default quick connection library from gha file." });

            major.DropDownItems.Add(new ToolStripMenuItem("Clear Library", null, (sender, e) => 
            { 
                StaticCreateObjectItems = new CreateObjectItems();
                SaveToJson();
            })
            { ToolTipText = "Clear the Library." });
            #endregion

            _canvasToolbar.Items.Add(button);
            ((ToolStripMenuItem)editor.MainMenuStrip.Items[1]).DropDownItems.Insert(16, major);
        }

        private static void CreateNumberBox(ToolStripMenuItem item, string itemName, string description, double originValue, Action<double> valueChange, double valueDefault, double Max, double Min, int decimalPlace = 0)
        {
            item.DropDown.Closing -= DropDown_Closing;
            item.DropDown.Closing += DropDown_Closing;

            CreateTextLabel(item, itemName, description + $"\nValue from {Min} to {Max}");

            GH_DigitScroller slider = new GH_DigitScroller
            {
                MinimumValue = (decimal)Min,
                MaximumValue = (decimal)Max,
                DecimalPlaces = decimalPlace,
                Value = (decimal)originValue,
                Size = new Size(150, 24),
            };
            slider.ValueChanged += Slider_ValueChanged;

            void Slider_ValueChanged(object sender, GH_DigitScrollerEventArgs e)
            {
                double result = (double)e.Value;
                result = result >= Min ? result : Min;
                result = result <= Max ? result : Max;
                slider.Value = (decimal)result;

                valueChange.Invoke(result);

            }

            GH_DocumentObject.Menu_AppendCustomItem(item.DropDown, slider);

            //Add a Reset Item.
            ToolStripMenuItem resetItem = new ToolStripMenuItem("Reset Value", Properties.Resources.ResetIcons_24);
            resetItem.Click += (sender, e) =>
            {
                slider.Value = (decimal)valueDefault;
                valueChange.Invoke(valueDefault);
            };
            item.DropDownItems.Add(resetItem);
        }
        private static void CreateTextLabel(ToolStripMenuItem item, string name, string tooltips = null)
        {
            ToolStripLabel textBox = new ToolStripLabel(name);
            textBox.TextAlign = ContentAlignment.MiddleCenter;
            textBox.Font = new Font(textBox.Font, FontStyle.Bold);
            if (!string.IsNullOrEmpty(tooltips))
                textBox.ToolTipText = tooltips;
            item.DropDownItems.Add(textBox);
        }
        private static void DropDown_Closing(object sender, ToolStripDropDownClosingEventArgs e)
        {
            e.Cancel = e.CloseReason == ToolStripDropDownCloseReason.ItemClicked;
        }
        private static void ActiveCanvas_MouseDown(object sender, MouseEventArgs e)
        {
            IGH_MouseInteraction activeInteraction = Instances.ActiveCanvas.ActiveInteraction;
            if (activeInteraction == null) return;

            if (UseQuickConnection && e.Button == MouseButtons.Left && activeInteraction is GH_WireInteraction)
            {
                if (GH_AdvancedWireInteraction._click)
                {
                    GH_AdvancedWireInteraction._click = false;
                    return;
                }

                Instances.ActiveCanvas.ActiveInteraction = new GH_AdvancedWireInteraction(activeInteraction.Owner,
                    new GH_CanvasMouseEvent(activeInteraction.Owner.Viewport, e), (IGH_Param)GH_AdvancedWireInteraction._sourceInfo.GetValue(activeInteraction));
            }
        }

        internal static bool ExchangeMethod(MethodInfo targetMethod, MethodInfo injectMethod)
        {
            if (targetMethod == null || injectMethod == null)
            {
                return false;
            }
            RuntimeHelpers.PrepareMethod(targetMethod.MethodHandle);
            RuntimeHelpers.PrepareMethod(injectMethod.MethodHandle);
            unsafe
            {
                if (IntPtr.Size == 4)
                {
                    int* tar = (int*)targetMethod.MethodHandle.Value.ToPointer() + 2;
                    int* inj = (int*)injectMethod.MethodHandle.Value.ToPointer() + 2;
                    var relay = *tar;
                    *tar = *inj;
                    *inj = relay;
                }
                else
                {
                    long* tar = (long*)targetMethod.MethodHandle.Value.ToPointer() + 1;
                    long* inj = (long*)injectMethod.MethodHandle.Value.ToPointer() + 1;
                    var relay = *tar;
                    *tar = *inj;
                    *inj = relay;
                }
            }
            return true;
        }
    }
}