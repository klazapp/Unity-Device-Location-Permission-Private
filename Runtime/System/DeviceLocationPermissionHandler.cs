using System;
using System.Collections;
using System.Runtime.CompilerServices;
using UnityEngine;

namespace com.Klazapp.Utility
{
    public class DeviceLocationPermissionHandler : MonoBehaviour, IDeviceLocationPermission
    {
        #region Variables
        [SerializeField]
        [Tooltip("Toggle this to set script's singleton status. Status will be set on script's OnAwake function")]
        private ScriptBehavior scriptBehavior = ScriptBehavior.None;
        public static DeviceLocationPermissionHandler Instance { get; private set; }
        
        //Callback
        private Action<bool, float, float, float, float> onDeviceLocationPermissionCallback;
        
        //Wfs
        private readonly WaitForSeconds oneWfs = new(1f);
        
        //Location accuracy
        private const float DESIRED_ACCURACY_IN_METERS = 1F;
        private const float UPDATE_DISTANCE_IN_METERS = 0.001F;
        #endregion
        
        #region Lifecycle Flow
        private void Awake()
        {
            SetScriptBehaviour(scriptBehavior);
        }
        #endregion
        
        #region Public Access
        public static bool GetDeviceLocationPermission()
        {
            return Input.location.isEnabledByUser;
        }
        
        public void RequestDeviceLocationPermission(Action<bool, float, float, float, float> deviceLocationPermissionCallback = null)
        {
            onDeviceLocationPermissionCallback = deviceLocationPermissionCallback;

            StartCoroutine(RequestLocationPermissionCo());

            IEnumerator RequestLocationPermissionCo()
            {
                //Start service before querying location
                Input.location.Start(DESIRED_ACCURACY_IN_METERS, UPDATE_DISTANCE_IN_METERS);

                //Wait until location initializes
                //Max wait time in seconds
                var maxWait = 5f; 

                while (Input.location.status == LocationServiceStatus.Initializing && maxWait > 0)
                {
                    yield return oneWfs;
                    maxWait--;
                }

                //Service did not initialize in 5 seconds
                if (maxWait < 1)
                {
                    DeviceCameraLocationCallback(false);
                    yield break;
                }

                //Connection has failed.
                if (Input.location.status == LocationServiceStatus.Failed)
                {
                    DeviceCameraLocationCallback(false);
                    yield break;
                }

                //Access granted and location value could be retrieved
                DeviceCameraLocationCallback(true, Input.location.lastData.latitude, Input.location.lastData.longitude, Input.location.lastData.altitude, Input.location.lastData.horizontalAccuracy);
            }
        }
        #endregion
        
        #region Modules
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void SetScriptBehaviour(ScriptBehavior behavior)
        {
            if (behavior is not (ScriptBehavior.Singleton or ScriptBehavior.PersistentSingleton)) 
                return;
            
            if (Instance != null && Instance != this)
            {
                Destroy(this.gameObject);
                return;
            }
                
            Instance = this;

            if (behavior == ScriptBehavior.PersistentSingleton)
            {
                DontDestroyOnLoad(this.gameObject);
            }
        }
        #endregion

        #region IDeviceLocationPermission
        public void DeviceCameraLocationCallback(bool isGranted, float latitude = 0f, float longitude = 0f, float altitude = 0f, float horizontalAccuracy = 0f)
        {
            onDeviceLocationPermissionCallback?.Invoke(isGranted, latitude, longitude, altitude, horizontalAccuracy);
        }
        #endregion
    }
}
