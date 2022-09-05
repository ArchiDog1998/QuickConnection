using Grasshopper;
using Grasshopper.Kernel;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace QuickConnection
{
    /// <summary>
    /// Interaction logic for SelectAssemblyWindow.xaml
    /// </summary>
    public partial class SelectAssemblyWindow : Window
    {
        public SelectAssemblyWindow()
        {
            InitializeComponent();

            var ids = Instances.ComponentServer.ObjectProxies.Select(p => p.LibraryGuid).ToArray();

            AssemList.ItemsSource = Instances.ComponentServer.Libraries.Where(l => !l.IsCoreLibrary && ids.Contains(l.Id));

            new WindowInteropHelper(this).Owner = Instances.DocumentEditor.Handle;

        }

        private void OKButton_Click(object sender, RoutedEventArgs e)
        {
            if(AssemList .SelectedItems != null)
            {
                List<Guid> ids = new List<Guid>(AssemList.SelectedItems.Count);

                foreach (var obj in AssemList.SelectedItems)
                {
                    if(!(obj is GH_AssemblyInfo info)) continue;
                    ids.Add(info.Id);
                }
                QuickConnectionAssemblyLoad.StaticCreateObjectItems.CreateDefaultStyle(false, ids.ToArray());
                QuickConnectionAssemblyLoad.SaveToJson();
            }

            this.Close();
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }
    }
}
