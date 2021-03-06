﻿using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using ReactNative.Bridge;
using System;
using System.IO;
using System.Threading.Tasks;
using Windows.Storage;

namespace ReactNative.Chakra.Executor
{
    /// <summary>
    /// Uses the native C++ interface to the JSRT for Chakra.
    /// </summary>
    public class NativeJavaScriptExecutor : IJavaScriptExecutor
    {
        private readonly ChakraBridge.NativeJavaScriptExecutor _executor;
        private readonly bool _useSerialization;

        /// <summary>
        /// Instantiates the <see cref="NativeJavaScriptExecutor"/>.
        /// </summary>
        /// <param name="useSerialization">true to use serialization, else false.</param>
        public NativeJavaScriptExecutor(bool useSerialization)
        {
            _useSerialization = useSerialization;
            _executor = new ChakraBridge.NativeJavaScriptExecutor();
            Native.ThrowIfError((JavaScriptErrorCode)_executor.InitializeHost());
        }

        /// <summary>
        /// Instantiates the <see cref="NativeJavaScriptExecutor"/>.
        /// </summary>
        public NativeJavaScriptExecutor() : this(false)
        {

        }

        /// <summary>
        /// Call the JavaScript method from the given module.
        /// </summary>
        /// <param name="moduleName">The module name.</param>
        /// <param name="methodName">The method name.</param>
        /// <param name="arguments">The arguments.</param>
        /// <returns>The flushed queue of native operations.</returns>
        public JToken CallFunctionReturnFlushedQueue(string moduleName, string methodName, JArray arguments)
        {
            if (moduleName == null)
                throw new ArgumentNullException(nameof(moduleName));
            if (methodName == null)
                throw new ArgumentNullException(nameof(methodName));
            if (arguments == null)
                throw new ArgumentNullException(nameof(arguments));

            var result = _executor.CallFunctionAndReturnFlushedQueue(moduleName, methodName, arguments.ToString(Formatting.None));
            Native.ThrowIfError((JavaScriptErrorCode)result.ErrorCode);

            return JToken.Parse(result.Result);
        }

        /// <summary>
        /// Disposes the <see cref="NativeJavaScriptExecutor"/> instance.
        /// </summary>
        public void Dispose()
        {
            Native.ThrowIfError((JavaScriptErrorCode)_executor.DisposeHost());
        }

        /// <summary>
        /// Flush the queue.
        /// </summary>
        /// <returns>The flushed queue of native operations.</returns>
        public JToken FlushedQueue()
        {
            var result = _executor.FlushedQueue();
            Native.ThrowIfError((JavaScriptErrorCode)result.ErrorCode);
            return JToken.Parse(result.Result);
        }

        /// <summary>
        /// Invoke the JavaScript callback.
        /// </summary>
        /// <param name="callbackId">The callback identifier.</param>
        /// <param name="arguments">The arguments.</param>
        /// <returns>The flushed queue of native operations.</returns>
        public JToken InvokeCallbackAndReturnFlushedQueue(int callbackId, JArray arguments)
        {
            if (arguments == null)
                throw new ArgumentNullException(nameof(arguments));

            var result = _executor.InvokeCallbackAndReturnFlushedQueue(callbackId, arguments.ToString(Formatting.None));
            Native.ThrowIfError((JavaScriptErrorCode)result.ErrorCode);
            return JToken.Parse(result.Result);
        }

        private void RunNormalScript(string script, string sourceUrl)
        {
            try
            {
                Native.ThrowIfError((JavaScriptErrorCode)_executor.RunScriptFromFile(script, sourceUrl));
            }
            catch (JavaScriptScriptException ex)
            {
                var jsonError = JavaScriptValueToJTokenConverter.Convert(ex.Error);
                var message = jsonError.Value<string>("message");
                var stackTrace = jsonError.Value<string>("stack");
                throw new Modules.Core.JavaScriptException(message ?? ex.Message, stackTrace, ex);
            }
        }

        private void RunSerializedScript(string script, string sourceUrl)
        {
            var localFolder = ApplicationData.Current.LocalFolder;
            var binFile = "ReactNativeSerializedBundle.bin";
            var binPath = Path.Combine(localFolder.Path, binFile);

            try
            {
                if(!EnsureSerializedScriptAsync(script, binFile).Result)
                {
                    Native.ThrowIfError((JavaScriptErrorCode)_executor.SerializeScriptFromFile(script, binPath));
                }

                Native.ThrowIfError((JavaScriptErrorCode)_executor.RunSerializedScriptFromFile(binPath, script, sourceUrl));
            }
            catch (JavaScriptScriptException ex)
            {
                var jsonError = JavaScriptValueToJTokenConverter.Convert(ex.Error);
                var message = jsonError.Value<string>("message");
                var stackTrace = jsonError.Value<string>("stack");
                throw new Modules.Core.JavaScriptException(message ?? ex.Message, stackTrace, ex);
            }
        }

        /// <summary>
        /// Runs the given script.
        /// </summary>
        /// <param name="script">The script.</param>
        /// <param name="sourceUrl">The source URL.</param>
        public void RunScript(string script, string sourceUrl)
        {
            if (script == null)
                throw new ArgumentNullException(nameof(script));
            if (sourceUrl == null)
                throw new ArgumentNullException(nameof(sourceUrl));

            if (_useSerialization)
            {
                RunSerializedScript(script, sourceUrl);
            }
            else
            {
                RunNormalScript(script, sourceUrl);
            }
        }

        private static async Task<bool> EnsureSerializedScriptAsync(string scriptFile, string binFile)
        {
            var localFolder = ApplicationData.Current.LocalFolder;

            var scriptItem = await StorageFile.GetFileFromPathAsync(scriptFile);
            var scriptItemProps = await scriptItem.GetBasicPropertiesAsync();
            var item = await localFolder.TryGetItemAsync(binFile);
            if (item != null)
            {
                var props = await item.GetBasicPropertiesAsync();
                return props.DateModified > scriptItemProps.DateModified;
            }

            return false;
        }

        /// <summary>
        /// Sets a global variable in the JavaScript runtime.
        /// </summary>
        /// <param name="propertyName">The global variable name.</param>
        /// <param name="value">The value.</param>
        public void SetGlobalVariable(string propertyName, JToken value)
        {
            if (propertyName == null)
                throw new ArgumentNullException(nameof(propertyName));
            if (value == null)
                throw new ArgumentNullException(nameof(value));

            Native.ThrowIfError(
                (JavaScriptErrorCode)_executor.SetGlobalVariable(propertyName, value.ToString(Formatting.None)));
        }
    }
}
