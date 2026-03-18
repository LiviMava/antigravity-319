using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;

namespace BplmSw
{
    public class TaskPanelVM : ViewModelBase
    {
        public ICommand LoginCommand { get; }
        public ICommand LogoutCommand { get; }
        public ICommand NewDocCommand { get; }
        public ICommand OpenDocCommand { get; }
        public ICommand CheckInCommand { get; }
        public ICommand CheckOutCommand { get; }

        // 按钮状态属性
        private bool _canCheckIn = false;
        private bool _canCheckOut = false;

        public bool CanCheckIn
        {
            get => _canCheckIn;
            set
            {
                if (_canCheckIn != value)
                {
                    _canCheckIn = value;
                    OnPropertyChanged();
                }
            }
        }

        public bool CanCheckOut
        {
            get => _canCheckOut;
            set
            {
                if (_canCheckOut != value)
                {
                    _canCheckOut = value;
                    OnPropertyChanged();
                }
            }
        }

        private BplmSwAddIn _addIn;

        public TaskPanelVM(BplmSwAddIn addIn)
        {
            _addIn = addIn;
            LoginCommand = new RelayCommand(Login);
            LogoutCommand = new RelayCommand(Logout);
            NewDocCommand = new RelayCommand(NewDoc);
            OpenDocCommand = new RelayCommand(OpenDoc);
            CheckInCommand = new RelayCommand(CheckIn);
            CheckOutCommand = new RelayCommand(CheckOut);
        }

        // 公共方法供BplmSwAddIn调用，同步按钮状态
        public void UpdateButtonState(bool canCheckIn, bool canCheckOut)
        {
            CanCheckIn = canCheckIn;
            CanCheckOut = canCheckOut;
        }

        private async void Login(object parameter)
        {
            await _addIn.HandleManualLoginAsync();
        }

        private void Logout(object parameter)
        {
            _addIn.HandleLogout();
        }
        private async void NewDoc(object parameter)
        {
            await _addIn.HandleNewDoc();
        }
        private async void OpenDoc(object parameter)
        {
            await _addIn.HandleOpenDoc();
        }
        private async void CheckIn(object parameter)
        {
            await _addIn.HandleCheckInDoc();
        }
        private async void CheckOut(object parameter)
        {
            await _addIn.HandleCheckOutDoc();
        }
    }
}