using System.Runtime.InteropServices;

namespace QuietCapture.Infrastructure.Windows.Audio;

public sealed class AudioEndpointEnumerator
{
    private const int S_OK = 0;
    private const int E_NOTFOUND =
        unchecked((int)0x80070490);

    private static readonly Guid MMDeviceEnumeratorClassId =
        new("BCDE0395-E52F-467C-8E3D-C4579291692E");

    private static readonly PropertyKey DeviceFriendlyNameKey =
        new(
            new Guid(
                "A45C254E-DF1C-4EFD-8020-67D146A850E0"),
            14);

    public IReadOnlyList<AudioEndpointSnapshot>
        EnumerateRenderEndpoints()
    {
        return Enumerate(
            AudioEndpointFlow.Render);
    }

    public IReadOnlyList<AudioEndpointSnapshot>
        EnumerateCaptureEndpoints()
    {
        return Enumerate(
            AudioEndpointFlow.Capture);
    }

    public AudioEndpointSnapshot?
        GetDefaultRenderEndpoint()
    {
        return GetDefault(
            AudioEndpointFlow.Render);
    }

    public AudioEndpointSnapshot?
        GetDefaultCaptureEndpoint()
    {
        return GetDefault(
            AudioEndpointFlow.Capture);
    }

    private static IReadOnlyList<AudioEndpointSnapshot>
        Enumerate(AudioEndpointFlow flow)
    {
        IMMDeviceEnumerator? enumerator = null;
        IMMDeviceCollection? collection = null;

        try
        {
            enumerator =
                CreateNativeEnumerator();

            int hr =
                enumerator.EnumAudioEndpoints(
                    ToNativeFlow(flow),
                    (uint)AudioEndpointState.Active,
                    out collection);

            ThrowIfFailed(
                hr,
                "Enumerating audio endpoints failed.");

            ThrowIfFailed(
                collection.GetCount(
                    out uint count),
                "Reading audio endpoint count failed.");

            var result =
                new List<AudioEndpointSnapshot>(
                    checked((int)count));

            for (uint index = 0;
                 index < count;
                 index++)
            {
                IMMDevice? device = null;

                try
                {
                    ThrowIfFailed(
                        collection.Item(
                            index,
                            out device),
                        "Reading an audio endpoint failed.");

                    AudioEndpointSnapshot snapshot =
                        CreateSnapshot(
                            device,
                            flow);

                    if (snapshot.IsActive)
                    {
                        result.Add(snapshot);
                    }
                }
                finally
                {
                    ReleaseComObject(device);
                }
            }

            return result
                .OrderBy(
                    endpoint => endpoint.Id,
                    StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }
        finally
        {
            ReleaseComObject(collection);
            ReleaseComObject(enumerator);
        }
    }

    private static AudioEndpointSnapshot?
        GetDefault(AudioEndpointFlow flow)
    {
        IMMDeviceEnumerator? enumerator = null;
        IMMDevice? device = null;

        try
        {
            enumerator =
                CreateNativeEnumerator();

            int hr =
                enumerator.GetDefaultAudioEndpoint(
                    ToNativeFlow(flow),
                    ERole.Multimedia,
                    out device);

            if (hr == E_NOTFOUND)
            {
                return null;
            }

            ThrowIfFailed(
                hr,
                "Resolving the default audio endpoint failed.");

            AudioEndpointSnapshot snapshot =
                CreateSnapshot(
                    device,
                    flow);

            return snapshot.IsActive
                ? snapshot
                : null;
        }
        finally
        {
            ReleaseComObject(device);
            ReleaseComObject(enumerator);
        }
    }

    private static AudioEndpointSnapshot CreateSnapshot(
        IMMDevice device,
        AudioEndpointFlow flow)
    {
        ThrowIfFailed(
            device.GetId(
                out string id),
            "Reading audio endpoint ID failed.");

        ThrowIfFailed(
            device.GetState(
                out uint state),
            "Reading audio endpoint state failed.");

        string? friendlyName =
            TryReadFriendlyName(device);

        return new AudioEndpointSnapshot(
            id,
            friendlyName,
            flow,
            (AudioEndpointState)state);
    }

    private static string? TryReadFriendlyName(
        IMMDevice device)
    {
        IPropertyStore? store = null;
        PropVariant value = default;

        try
        {
            int hr =
                device.OpenPropertyStore(
                    StorageAccessMode.Read,
                    out store);

            if (hr != S_OK ||
                store is null)
            {
                return null;
            }

            PropertyKey key =
                DeviceFriendlyNameKey;

            hr =
                store.GetValue(
                    ref key,
                    out value);

            if (hr != S_OK ||
                value.VariantType !=
                    VariantType.StringPointer ||
                value.PointerValue ==
                    IntPtr.Zero)
            {
                return null;
            }

            return Marshal.PtrToStringUni(
                value.PointerValue);
        }
        finally
        {
            if (value.VariantType !=
                VariantType.Empty)
            {
                _ = PropVariantClear(
                    ref value);
            }

            ReleaseComObject(store);
        }
    }

    private static IMMDeviceEnumerator
        CreateNativeEnumerator()
    {
        Type type =
            Type.GetTypeFromCLSID(
                MMDeviceEnumeratorClassId,
                throwOnError: true)
            ?? throw new InvalidOperationException(
                "Windows Core Audio endpoint enumerator is unavailable.");

        object instance =
            Activator.CreateInstance(type)
            ?? throw new InvalidOperationException(
                "Windows Core Audio endpoint enumerator could not be created.");

        return (IMMDeviceEnumerator)instance;
    }

    private static EDataFlow ToNativeFlow(
        AudioEndpointFlow flow)
    {
        return flow switch
        {
            AudioEndpointFlow.Render =>
                EDataFlow.Render,

            AudioEndpointFlow.Capture =>
                EDataFlow.Capture,

            _ =>
                throw new ArgumentOutOfRangeException(
                    nameof(flow))
        };
    }

    private static void ThrowIfFailed(
        int hresult,
        string message)
    {
        if (hresult >= 0)
        {
            return;
        }

        Exception? inner =
            Marshal.GetExceptionForHR(
                hresult);

        throw new InvalidOperationException(
            message,
            inner);
    }

    private static void ReleaseComObject(
        object? value)
    {
        if (value is not null &&
            Marshal.IsComObject(value))
        {
            _ = Marshal.FinalReleaseComObject(
                value);
        }
    }

    private enum EDataFlow
    {
        Render = 0,
        Capture = 1,
        All = 2
    }

    private enum ERole
    {
        Console = 0,
        Multimedia = 1,
        Communications = 2
    }

    private enum StorageAccessMode
    {
        Read = 0
    }

    private enum VariantType : ushort
    {
        Empty = 0,
        StringPointer = 31
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct PropertyKey
    {
        public PropertyKey(
            Guid formatId,
            uint propertyId)
        {
            FormatId = formatId;
            PropertyId = propertyId;
        }

        public Guid FormatId;
        public uint PropertyId;
    }

    [StructLayout(
        LayoutKind.Explicit,
        Size = 24)]
    private struct PropVariant
    {
        [FieldOffset(0)]
        public VariantType VariantType;

        [FieldOffset(8)]
        public IntPtr PointerValue;
    }

    [ComImport]
    [Guid("A95664D2-9614-4F35-A746-DE8DB63617E6")]
    [InterfaceType(
        ComInterfaceType.InterfaceIsIUnknown)]
    private interface IMMDeviceEnumerator
    {
        [PreserveSig]
        int EnumAudioEndpoints(
            EDataFlow dataFlow,
            uint stateMask,
            out IMMDeviceCollection devices);

        [PreserveSig]
        int GetDefaultAudioEndpoint(
            EDataFlow dataFlow,
            ERole role,
            out IMMDevice device);

        [PreserveSig]
        int GetDevice(
            [MarshalAs(UnmanagedType.LPWStr)]
            string id,
            out IMMDevice device);

        [PreserveSig]
        int RegisterEndpointNotificationCallback(
            IntPtr client);

        [PreserveSig]
        int UnregisterEndpointNotificationCallback(
            IntPtr client);
    }

    [ComImport]
    [Guid("0BD7A1BE-7A1A-44DB-8397-C0A8FEF6B3D4")]
    [InterfaceType(
        ComInterfaceType.InterfaceIsIUnknown)]
    private interface IMMDeviceCollection
    {
        [PreserveSig]
        int GetCount(
            out uint count);

        [PreserveSig]
        int Item(
            uint index,
            out IMMDevice device);
    }

    [ComImport]
    [Guid("D666063F-1587-4E43-81F1-B948E807363F")]
    [InterfaceType(
        ComInterfaceType.InterfaceIsIUnknown)]
    private interface IMMDevice
    {
        [PreserveSig]
        int Activate(
            ref Guid interfaceId,
            uint classContext,
            IntPtr activationParameters,
            out IntPtr interfacePointer);

        [PreserveSig]
        int OpenPropertyStore(
            StorageAccessMode accessMode,
            out IPropertyStore properties);

        [PreserveSig]
        int GetId(
            [MarshalAs(UnmanagedType.LPWStr)]
            out string id);

        [PreserveSig]
        int GetState(
            out uint state);
    }

    [ComImport]
    [Guid("886D8EEB-8CF2-4446-8D02-CDBA1DBDCF99")]
    [InterfaceType(
        ComInterfaceType.InterfaceIsIUnknown)]
    private interface IPropertyStore
    {
        [PreserveSig]
        int GetCount(
            out uint count);

        [PreserveSig]
        int GetAt(
            uint propertyIndex,
            out PropertyKey key);

        [PreserveSig]
        int GetValue(
            ref PropertyKey key,
            out PropVariant value);

        [PreserveSig]
        int SetValue(
            ref PropertyKey key,
            ref PropVariant value);

        [PreserveSig]
        int Commit();
    }

    [DllImport("ole32.dll")]
    private static extern int PropVariantClear(
        ref PropVariant value);
}
