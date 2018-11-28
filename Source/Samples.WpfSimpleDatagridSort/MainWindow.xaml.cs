using System.Collections.Generic;
using System.Linq;
using System.Windows;
using ChangeTracking;

namespace Samples.WpfSimpleDatagridSort
{
    public class User
    {
        public virtual string Firstname { get; set; }
        public virtual string Lastname { get; set; }
    }

    public partial class MainWindow : Window
    {

        public MainWindow()
        {
            InitializeComponent();
            var list = new List<User>();
            list.Add(new User {Firstname = "fn1", Lastname = "ln1"});
            list.Add(new User { Firstname = "fn2", Lastname = "ln2" });
            DataGrid.ItemsSource = list.AsTrackable().CastToIChangeTrackableCollection();
        }

        private void CheckChanges_OnClick(object sender, RoutedEventArgs e)
        {
            var trackable = (IChangeTrackableCollection<User>) DataGrid.ItemsSource;
            MessageBox.Show($"Added items: {trackable.AddedItems.Count()}\nDeleted: {trackable.DeletedItems.Count()}" +
                            $"\nModified: {trackable.ChangedItems.Count()}\nUnchanged: {trackable.UnchangedItems.Count()}");
        }
    }
}
