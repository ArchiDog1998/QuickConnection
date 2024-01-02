using Grasshopper;
using Grasshopper.GUI;
using Grasshopper.GUI.Canvas.Interaction;
using Grasshopper.Kernel;
using Newtonsoft.Json;
using SimpleGrasshopper.Attributes;
using System;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows.Forms;

namespace QuickConnection;

public class QuickConnectionInfo : GH_AssemblyInfo
{
    public override string Name => "Quick Connection";

    //Return a 24x24 pixel bitmap to represent this GHA library.
    public override Bitmap Icon => Properties.Resources.QuickwireIcon_24;

    //Return a short string describing the purpose of this GHA library.
    public override string Description => "Fast connecting wires.";

    public override Guid Id => new("6C0DDB78-4484-4481-996A-60CF4D9B90CE");

    //Return a string identifying you or your company.
    public override string AuthorName => "秋水";

    //Return a string representing your preferred contact details.
    public override string AuthorContact => "1123993881@qq.com";

    public override string Version => "1.0.7";
}

partial class SimpleAssemblyPriority
{
    private static readonly string _location = Path.Combine(Folders.SettingsFolder, "quickwires.json");
    internal static CreateObjectItems StaticCreateObjectItems { get; private set; } = new();

    protected override int? MenuIndex => 1;
    protected override int InsertIndex => 16;

    [Setting, Config("Quick Connection"), ToolButton("QuickwireIcon_24.png")]
    private static readonly bool _useQuickConnection = true;

    [Setting, Config("Use Fuzzy Connection")]
    private static readonly bool _useFuzzyConnection = true;

    [Setting, Config("Click Wait Time", "The time from the first down to the first up of the mouse is less than this value, then it is considered a click.", section:1)]
    private static readonly int _quickConnectionMaxWaitTime = 100;

    [Setting, Config("Can't Wire Easily", "If you can't wire functionally, please check it.", section: 1)]
    private static readonly bool _cantWireEasily = false;

    [Setting]
    private static readonly double _quickConnectionWindowWidth = 200;

    [Setting]
    private static readonly double _quickConnectionWindowHeight = 200;

    [Config("Set Core Only Library", "Click to set the quick connection library to all core document objects.", section:2)]
    public static object CoreLib
    {
        get => true;
        set
        {
            StaticCreateObjectItems.CreateDefaultStyle(true);
            SaveToJson();
        }
    }

    [Config("Set All Library", "Click to set the quick connection library to all document objects.", section: 2)]
    public static object AllLib
    {
        get => true;
        set
        {
            StaticCreateObjectItems.CreateDefaultStyle(false);
            SaveToJson();
        }
    }

    [Config("Set Selected Library", "Click to set quick connection library to the document objects from selected gha file.", section: 2)]
    public static object SelectedLib
    {
        get => true;
        set
        {
            new SelectAssemblyWindow().Show();
        }
    }

    [Config("Load Default Library", "Click to load default quick connection library from this plugins' file.", section: 2)]
    public static object DefaultLib
    {
        get => true;
        set
        {
            LoadFromLocal();
            SaveToJson();
        }
    }

    [Config("Clear Library", "Clear the Library.", section: 2)]
    public static object ClearLib
    {
        get => true;
        set
        {
            StaticCreateObjectItems = new CreateObjectItems();
            SaveToJson();
        }
    }

    protected override void DoWithEditor(GH_DocumentEditor editor)
    {
        //Read from json.
        try
        {
            if (File.Exists(_location))
            {
                string jsonStr = File.ReadAllText(_location);
                try
                {
                    StaticCreateObjectItems = new CreateObjectItems(JsonConvert.DeserializeObject<CreateObjectItemsSave>(jsonStr));
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

        base.DoWithEditor(editor);
    }

    private static void ActiveCanvas_MouseDown(object sender, MouseEventArgs e)
    {
        IGH_MouseInteraction activeInteraction = Instances.ActiveCanvas.ActiveInteraction;
        if (activeInteraction == null) return;

        if (UseQuickConnection && e.Button == MouseButtons.Left)
        {
            if (GH_AdvancedWireInteraction._click != PointF.Empty)
            {
                if (GH_AdvancedWireInteraction.DistanceTo(activeInteraction.CanvasPointDown, GH_AdvancedWireInteraction._click) < 10)
                {
                    //GH_AdvancedWireInteraction._click.Reset();
                    return;
                }
                GH_AdvancedWireInteraction._click = PointF.Empty;
            }

            if (activeInteraction is GH_WireInteraction)
            {
                Instances.ActiveCanvas.ActiveInteraction = new GH_AdvancedWireInteraction(activeInteraction.Owner,
                    new GH_CanvasMouseEvent(activeInteraction.Owner.Viewport, e), (IGH_Param)GH_AdvancedWireInteraction._sourceInfo.GetValue(activeInteraction));
            }
            else if (activeInteraction is GH_RewireInteraction)
            {
                Instances.ActiveCanvas.ActiveInteraction = new GH_AdvancedRewireInteraction(activeInteraction.Owner,
                    new GH_CanvasMouseEvent(activeInteraction.Owner.Viewport, e), (IGH_Param)GH_AdvancedRewireInteraction._sourceInfo.GetValue(activeInteraction));
            }
        }
    }


    private static void LoadFromLocal()
    {
        StaticCreateObjectItems = new CreateObjectItems(JsonConvert.DeserializeObject<CreateObjectItemsSave>(Properties.Resources.quickwires));
    }

    internal static void SaveToJson()
    {
        try
        {
            File.WriteAllText(_location, JsonConvert.SerializeObject(new CreateObjectItemsSave(StaticCreateObjectItems)));
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "Json Library Save Failed");
        }
    }


}
