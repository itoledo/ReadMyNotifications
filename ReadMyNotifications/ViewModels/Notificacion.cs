using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.UI.Xaml.Media.Imaging;
using MyToolkit.Model;

namespace ReadMyNotifications.ViewModels
{
    public class Notificacion : ObservableObject
    {
        public uint Id;
        public DateTimeOffset CreationTime;

        private BitmapImage _logo;
        public BitmapImage Logo { get { return _logo; } set { _logo = value; RaisePropertyChanged(() => Logo); } }
        private string _appName;
        public string AppName { get { return _appName; } set { _appName = value; RaisePropertyChanged(() => AppName); } }
        private string _title;
        public string Title { get { return _title;  } set { _title = value; RaisePropertyChanged(() => Title); } }
        private string _texto;
        public string Text { get { return _texto; } set { _texto = value; RaisePropertyChanged(() => Text); } }
    }
}
