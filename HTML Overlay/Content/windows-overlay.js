if (window.CefSharp) {
  // Bind to WindowsOverlay from C#
  (async function() { await CefSharp.BindObjectAsync("WindowsOverlay"); })();

  // Give us broadcasts
  window.client.on('General.Custom', (message) => {
    if (!("data" in message)) return;

    // Discard broadcasts not meant for us
    if (message.data.api !== "html-windows-overlay")
    return;

    // Ensure this is an API call
    if (!("call" in message.data) || !("args" in message.data))
    return;

    console.log(`API: ${message.data.call}, args: ${message.data.args.toString()}`);

    WindowsOverlay[message.data.call].apply(null, message.data.args);
  })
}