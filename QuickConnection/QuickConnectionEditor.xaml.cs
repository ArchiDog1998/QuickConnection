using Grasshopper;
using Grasshopper.GUI.Canvas;
using Grasshopper.Kernel;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media.Imaging;
using Point = System.Drawing.Point;

namespace QuickConnection;

/// <summary>
/// Interaction logic for QuickWireEditor.xaml
/// </summary>
public partial class QuickConnectionEditor : Window
{
    private readonly GH_Canvas _canvas = Instances.ActiveCanvas;
    private bool _isTheSame = false;
    private IGH_Param _targetParam;
    private IGH_Param TargetParam 
    {
        get => _targetParam;
        set
        {
            _targetParam = value;
            if (_targetParam == null) return;
            _isTheSame = _isTheSameFunc(_targetParam);
        }
    }

    private readonly bool _isInput;
    //private Guid _componentGuid;
    private readonly CreateObjectItem[] _preList;
    private bool _cancel = false;
    private readonly Func<IGH_Param, bool> _isTheSameFunc;
    private readonly Action<CreateObjectItem[], bool> _saveValue;

    public QuickConnectionEditor(bool isInput, Bitmap icon, string name, ObservableCollection<CreateObjectItem> structureLists, Func<IGH_Param, bool> isTheSame,
        Action<CreateObjectItem[], bool> saveValue)
    {
        this._isInput = isInput;
        this.DataContext = structureLists;
        this._preList = [.. structureLists];

        MemoryStream ms = new MemoryStream();
        icon.Save(ms, System.Drawing.Imaging.ImageFormat.Png);

        BitmapImage ImageIcon = new BitmapImage();
        ImageIcon.BeginInit();
        ms.Seek(0, SeekOrigin.Begin);
        ImageIcon.StreamSource = ms;
        ImageIcon.EndInit();
        Icon = ImageIcon;

        _isTheSameFunc = isTheSame;
        _saveValue = saveValue;
        InitializeComponent();

        this.Title += "-" + name + (isInput ? "[In]" : "[Out]");

        new WindowInteropHelper(this).Owner = Instances.DocumentEditor.Handle;
    }
    private void RemoveButton_Click(object sender, RoutedEventArgs e)
    {
        int index = dataGrid.SelectedIndex;
        if (index == -1) return;
        ((ObservableCollection<CreateObjectItem>)DataContext).RemoveAt(index);
        dataGrid.SelectedIndex = Math.Min(index, ((ObservableCollection<CreateObjectItem>)DataContext).Count - 1);
    }  

    private void UpButton_Click(object sender, RoutedEventArgs e)
    {
        int index = dataGrid.SelectedIndex;
        if (index < 1) return;
        int changeindex = index - 1;
        ChangeIndex(index, changeindex);
        dataGrid.SelectedIndex = changeindex;
    }
    private void DownButton_Click(object sender, RoutedEventArgs e)
    {
        int index = dataGrid.SelectedIndex;
        if (index < 0) return;
        if (index == ((ObservableCollection<CreateObjectItem>)DataContext).Count - 1) return;
        int changeindex = index + 1;
        ChangeIndex(index, changeindex);
        dataGrid.SelectedIndex = changeindex;
    }

    private void ChangeIndex(int a, int b)
    {
        ObservableCollection<CreateObjectItem> lt = (ObservableCollection<CreateObjectItem>)DataContext;
        (lt[b], lt[a]) = (lt[a], lt[b]);
    }

    private void OKButton_Click(object sender, RoutedEventArgs e)
    {
        this.Close();
    }

    private void ApplyButton_Click(object sender, RoutedEventArgs e)
    {
        Apply();
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        _cancel = true;
        this.Close();
    }

    private void Apply()
    {
        CreateObjectItem[] saveItems = [.. ((ObservableCollection<CreateObjectItem>)DataContext)];
        _saveValue(saveItems, _isInput);
    }

    protected override void OnClosed(EventArgs e)
    {
        if (_cancel)
        {
            _saveValue(_preList, _isInput);
        }
        else
        {
            Apply();
            SimpleAssemblyPriority.SaveToJson();
        }
        base.OnClosed(e);
    }

    private void AddButton_MouseDown(object sender, MouseButtonEventArgs e)
    {
        _canvas.MouseUp -= Canvas_MouseUp;
        _canvas.MouseLeave -= Canvas_MouseLeave;
        _canvas.MouseMove -= Canvas_MouseMove;
        _canvas.CanvasPostPaintWidgets -= CanvasPostPaintWidgets;

        _canvas.MouseUp += Canvas_MouseUp;
        _canvas.MouseMove += Canvas_MouseMove;
        _canvas.MouseLeave += Canvas_MouseLeave;
        _canvas.CanvasPostPaintWidgets += CanvasPostPaintWidgets;
        _canvas.ModifiersEnabled = false;
        _canvas.Refresh();
    }

    private void Canvas_MouseLeave(object sender, EventArgs e)
    {
        Finish();
        TargetParam = null;
    }

    private void Canvas_MouseUp(object sender, System.Windows.Forms.MouseEventArgs e)
    {
        Finish();
        if (TargetParam != null) SaveOne(TargetParam);
        TargetParam = null;
    }

    private void Finish()
    {
        _canvas.MouseUp -= Canvas_MouseUp;
        _canvas.MouseLeave -= Canvas_MouseLeave;
        _canvas.MouseMove -= Canvas_MouseMove;
        _canvas.CanvasPostPaintWidgets -= CanvasPostPaintWidgets;
        Instances.CursorServer.ResetCursor(_canvas);
        _canvas.ModifiersEnabled = true;
        _canvas.Refresh();
    }

    private void Canvas_MouseMove(object sender, System.Windows.Forms.MouseEventArgs e)
    {
        Instances.CursorServer.AttachCursor(_canvas, "GH_Target");
        PointF pt = _canvas.Viewport.UnprojectPoint(e.Location);
        GH_RelevantObjectData gH_RelevantObjectData = _canvas.Document.RelevantObjectAtPoint(pt, GH_RelevantObjectFilter.Attributes);
        IGH_Param param = null;
        if (gH_RelevantObjectData != null)
        {
            if(gH_RelevantObjectData.Parameter != null)
            {
                param = gH_RelevantObjectData.Parameter;
                if (TargetParam == param) return;
                if ((_isInput && param.Kind != GH_ParamKind.input) || (!_isInput && param.Kind != GH_ParamKind.output))
                {
                    TargetParam = param;
                    _canvas.Refresh();
                }
            }
            else
            {
                IGH_DocumentObject obj = gH_RelevantObjectData.Object;
                if (obj == null) return;
                IGH_Component component = (IGH_Component)obj;
                if(component == null) return;

                List<IGH_Param> paramLt = null;
                if (_isInput)
                {
                    if(component.Params.Output.Count == 0) return;
                    paramLt = component.Params.Output;

                }
                else if (!_isInput)
                {
                    if (component.Params.Input.Count == 0) return;
                    paramLt = component.Params.Input;
                }

                if (paramLt == null) return;
                float minDis = float.MaxValue;
                foreach (var item in paramLt)
                {
                    PointF pt2 = item.Attributes.Pivot;
                    float distance = (float)Math.Sqrt(Math.Pow(pt.X - pt2.X, 2) + Math.Pow(pt.Y - pt2.Y, 2));
                    if(distance < minDis)
                    {
                        minDis = distance;
                        param = item;
                    }
                }

                if (param == TargetParam) return;
                TargetParam = param;
                _canvas.Refresh();

            }
        }
    }

    private void SaveOne(IGH_Param param)
    {
        CreateObjectItem item;
        if (param.Kind == GH_ParamKind.floating)
        {
            item = new CreateObjectItem(param.ComponentGuid, 0, null, _isInput);
        }
        else
        {
            IGH_DocumentObject obj = param.Attributes.GetTopLevel.DocObject;
            if (obj is not IGH_Component) return;
            IGH_Component com = (GH_Component)obj;

            int index = _isInput ? com.Params.Output.IndexOf(param) : com.Params.Input.IndexOf(param);
            item = new CreateObjectItem(com.ComponentGuid, (ushort)index, null, _isInput);

        }
        ((ObservableCollection<CreateObjectItem>)DataContext).Add(item);
        dataGrid.SelectedIndex = ((ObservableCollection<CreateObjectItem>)DataContext).Count - 1;
    }

    private void CanvasPostPaintWidgets(GH_Canvas canvas)
    {
        System.Drawing.Drawing2D.Matrix transform = canvas.Graphics.Transform;
        canvas.Graphics.ResetTransform();
        System.Drawing.Rectangle clientRectangle = canvas.ClientRectangle;
        clientRectangle.Inflate(5, 5);
        Region region = new Region(clientRectangle);
        System.Drawing.Rectangle rect = System.Drawing.Rectangle.Empty;
        if (TargetParam != null)
        {
            RectangleF bounds = TargetParam.Attributes.Bounds;
            rect = GH_Convert.ToRectangle(canvas.Viewport.ProjectRectangle(bounds));
            switch (TargetParam.Kind)
            {
                case GH_ParamKind.input:
                    rect.Inflate(2, 2);
                    break;
                case GH_ParamKind.output:
                    rect.Inflate(2, 2);
                    break;
                case GH_ParamKind.floating:
                    rect.Inflate(0, 0);
                    break;
            }
            region.Exclude(rect);
        }
        SolidBrush solidBrush = new SolidBrush(Color.FromArgb(180, Color.White));
        canvas.Graphics.FillRegion(solidBrush, region);
        solidBrush.Dispose();
        region.Dispose();
        if (TargetParam != null)
        {
            Color color = _isTheSame ? Color.OliveDrab : Color.Chocolate;

            canvas.Graphics.DrawRectangle(new Pen(color), rect);
            Pen pen = new Pen(color, 3f);
            int num = 6;
            int num2 = rect.Left - 4;
            int num3 = rect.Right + 4;
            int num4 = rect.Top - 4;
            int num5 = rect.Bottom + 4;
            canvas.Graphics.DrawLines(pen, new Point[3]
            {
                new (num2 + num, num4),
                new (num2, num4),
                new (num2, num4 + num)
            });
            canvas.Graphics.DrawLines(pen, new Point[3]
            {
                new (num3 - num, num4),
                new (num3, num4),
                new (num3, num4 + num)
            });
            canvas.Graphics.DrawLines(pen, new Point[3]
            {
                new (num2 + num, num5),
                new (num2, num5),
                new (num2, num5 - num)
            });
            canvas.Graphics.DrawLines(pen, new Point[3]
            {
                new (num3 - num, num5),
                new (num3, num5),
                new (num3, num5 - num)
            });
            pen.Dispose();
        }
        canvas.Graphics.Transform = transform;
    }
}

[ValueConversion(typeof(int), typeof(bool))]
public class GridSelectedConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value == null) return null;

        int grid = (int)value;

        return grid != -1;

    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return null;
    }
}

[ValueConversion(typeof(System.Drawing.Bitmap), typeof(BitmapImage))]
public class BitmapConverter : IValueConverter
{
    public static BitmapImage ToImageSource(Bitmap bitmap)
    {
        MemoryStream ms = new MemoryStream();
        bitmap.Save(ms, System.Drawing.Imaging.ImageFormat.Png);
        BitmapImage image = new BitmapImage();

        image.BeginInit();
        ms.Seek(0, SeekOrigin.Begin);
        image.StreamSource = ms;
        image.EndInit();
        return image;
    }

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value == null) return null;

        System.Drawing.Bitmap picture = (System.Drawing.Bitmap)value;
        return ToImageSource(picture);
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return null;
    }
}
