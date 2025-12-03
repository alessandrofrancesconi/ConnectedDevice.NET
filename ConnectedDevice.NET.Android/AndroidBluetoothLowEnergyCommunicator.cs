using Android;
using Android.Bluetooth;
using Android.Content;
using Android.Content.PM;
using Android.Locations;
using Android.Provider;
using AndroidX.Core.App;
using AndroidX.Core.Content;
using ConnectedDevice.NET.Communication;
using ConnectedDevice.NET.Communication.Protocol;
using ConnectedDevice.NET.Exceptions;
using ConnectedDevice.NET.Models;
using Javax.Crypto;
using Microsoft.Extensions.Logging;
using Plugin.BLE;
using Plugin.BLE.Abstractions.Contracts;
using System.Reflection;

namespace ConnectedDevice.NET.Android
{
    public class AndroidBluetoothLowEnergyCommunicatorParams : BluetoothLowEnergyCommunicatorParams
    {
        public bool RequestPermission = true;
        public bool CheckLocationSettings = true;
        public Func<Activity> GetCurrentActivityMethod = null;
        public Action<Action> RunOnUIThreadMethod = null;
    }

    public class AndroidBluetoothLowEnergyCommunicator : BluetoothLowEnergyCommunicator
    {
        public const int BLUETOOTH_PERMISSIONS_REQUEST_CODE = 0xCDB1; //ConnectedDeviceBluetooth1
        
        public EventHandler<EventArgs>? OnLocationServiceDisabled;

        public AndroidBluetoothLowEnergyCommunicator(AndroidBluetoothLowEnergyCommunicatorParams? p = null) : base(CrossBluetoothLE.Current, p ?? AndroidBluetoothLowEnergyCommunicatorParams.Default)
        {
            var andParams = (AndroidBluetoothLowEnergyCommunicatorParams)this.Params;
            if (andParams.RequestPermission && andParams.GetCurrentActivityMethod == null) throw new InvalidOperationException("GetCurrentActivityMethod must be set when RequestPermission is true.");
        }

        private void CheckPermissions()
        {
            if (!this.RequestBluetoothPermissions()) throw new MissingPermissionException("Some permissions are required.");
            if (this.GetAdapterState() == AdapterState.OFF) throw new AdapterDisabledException("Bluetooth adapter is disabled.");
            if (!this.CheckLocationSettings()) throw new LocationServiceDisabledException("Location service is disabled.");
        }

        private bool RequestBluetoothPermissions()
        {
            var andParams = (AndroidBluetoothLowEnergyCommunicatorParams)this.Params;
            if (!andParams.RequestPermission) return true;

            var permissionsStatus = new Dictionary<string, Permission>();
            if (OperatingSystem.IsAndroidVersionAtLeast(31))
            {
                permissionsStatus.Add(Manifest.Permission.BluetoothScan, ContextCompat.CheckSelfPermission(Application.Context, Manifest.Permission.BluetoothScan));
                permissionsStatus.Add(Manifest.Permission.BluetoothConnect, ContextCompat.CheckSelfPermission(Application.Context, Manifest.Permission.BluetoothConnect));
                permissionsStatus.Add(Manifest.Permission.BluetoothAdmin, ContextCompat.CheckSelfPermission(Application.Context, Manifest.Permission.BluetoothAdmin));
            }
            else
            {
                permissionsStatus.Add(Manifest.Permission.Bluetooth, ContextCompat.CheckSelfPermission(Application.Context, Manifest.Permission.Bluetooth));
                permissionsStatus.Add(Manifest.Permission.BluetoothAdmin, ContextCompat.CheckSelfPermission(Application.Context, Manifest.Permission.BluetoothAdmin));
                permissionsStatus.Add(Manifest.Permission.AccessFineLocation, ContextCompat.CheckSelfPermission(Application.Context, Manifest.Permission.AccessFineLocation));
            }

            var toBeGranted = permissionsStatus.Where(i => i.Value != Permission.Granted).Select(i => i.Key);
            if (toBeGranted.Count() > 0)
            {
                this.PrintLog(LogLevel.Warning, "Some permission are not granted, sending request for [{0}]", string.Join(',', toBeGranted));
                ActivityCompat.RequestPermissions(
                    andParams.GetCurrentActivityMethod?.Invoke(),
                    toBeGranted.ToArray(),
                    BLUETOOTH_PERMISSIONS_REQUEST_CODE);

                return false;
            }

            return true;
        }

        protected override async Task ConnectToDeviceNative(RemoteDevice dev, CancellationToken cToken = default)
        {
            try
            {
                this.CheckPermissions();
            }
            catch (Exception e)
            {
                var args = new ConnectionChangedEventArgs(this, ConnectionState.DISCONNECTED, e);
                this.RaiseConnectionChangedEvent(args);
                return;
            }

            await base.ConnectToDeviceNative(dev, cToken);
        }

        public override async Task DiscoverDevices(CancellationToken cToken = default)
        {
            try
            {
                this.CheckPermissions();
            }
            catch (Exception e)
            {
                var args = new DiscoverDevicesFinishedEventArgs(this, e);
                this.RaiseDiscoverDevicesFinishedEvent(args);
                return;
            }

            await base.DiscoverDevices(cToken);
        }

        public override AdapterState GetAdapterState()
        {
            var andParams = (AndroidBluetoothLowEnergyCommunicatorParams)this.Params;
            var bluetoothManager = andParams.GetCurrentActivityMethod()?.ApplicationContext?.GetSystemService(Context.BluetoothService) as BluetoothManager;
            if (bluetoothManager == null || bluetoothManager.Adapter == null) return AdapterState.MISSING;
            var a = bluetoothManager.Adapter;
            if (a.IsEnabled) return AdapterState.ON;
            else return AdapterState.OFF;
        }

        private bool CheckLocationSettings()
        {
            var andParams = (AndroidBluetoothLowEnergyCommunicatorParams)this.Params;
            if (!andParams.CheckLocationSettings) return true;

            if (!OperatingSystem.IsAndroidVersionAtLeast(31))
            {
                bool locationEnabled = true;
                if (OperatingSystem.IsAndroidVersionAtLeast(28))
                {
                    LocationManager lm = (LocationManager)Application.Context.GetSystemService(Context.LocationService);
                    locationEnabled = lm.IsLocationEnabled;
                }
                else
                {
                    int mode = Settings.Secure.GetInt(Application.Context.ContentResolver, Settings.Secure.LocationMode, (int)SecurityLocationMode.Off);
                    locationEnabled = (mode != (int)SecurityLocationMode.Off);
                }

                if (!locationEnabled)
                {
                    this.PrintLog(LogLevel.Warning, "Location Service is disabled.");
                    return false;
                }
            }
            return true;
        }

        protected override async Task SendDataNative(ClientMessage message)
        {
            ICharacteristic? characteristic;
            if (this.WriteCharacteristicToUse != null) characteristic = this.WriteCharacteristicToUse;
            else characteristic = this.WriteCharacteristics.FirstOrDefault();

            if (characteristic == null) throw new NullReferenceException("Write characteristic is not set. Cannot send data.");

            var tcs = new TaskCompletionSource();
            Action writeAction = async () =>
            {
                try
                {
                    var res = await characteristic.WriteAsync(message.Data);
                    if (res != 0) tcs.SetException(new Exception("Bluetooth sent error with code " + res));
                    else tcs.SetResult();
                }
                catch (Exception ex)
                {
                    this.PrintLog(LogLevel.Error, "Error sending data: '{0}'", ex.Message);
                    tcs.SetException(ex);
                }
                finally
                {
                    // reset WriteChar to use
                    this.WriteCharacteristicToUse = null;
                }
            };

            // if a method to run the Action on the UI thread has been given, use it
            var andParams = (AndroidBluetoothLowEnergyCommunicatorParams)this.Params;
            if (andParams.RunOnUIThreadMethod != null) andParams.RunOnUIThreadMethod.Invoke(writeAction);
            else writeAction.Invoke();

            await tcs.Task;
        }
    }
}