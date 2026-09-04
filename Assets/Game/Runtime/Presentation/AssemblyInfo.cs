using System.Runtime.CompilerServices;

// The editor-side screen builders assign the serialized references on these views. Keeping the
// fields internal rather than public means nothing at runtime can reach in and rewire a screen.
[assembly: InternalsVisibleTo("DJMaximusKaiserSoje.Editor")]

// The smoke tests read back what a screen is currently showing.
[assembly: InternalsVisibleTo("DJMaximusKaiserSoje.Tests.PlayMode")]
[assembly: InternalsVisibleTo("DJMaximusKaiserSoje.Tests.EditMode")]
