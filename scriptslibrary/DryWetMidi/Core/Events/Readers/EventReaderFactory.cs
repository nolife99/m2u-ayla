namespace Melanchall.DryWetMidi.Core
{
    /// <summary>
    /// Provides a way to get <see cref="IEventReader"/> for an event.
    /// </summary>
    internal static class EventReaderFactory
    {
        #region Fields

        private static readonly IEventReader MetaEventReader = new MetaEventReader();
        private static readonly IEventReader ChannelEventReader = new ChannelEventReader();

        #endregion

        #region Methods

        /// <summary>
        /// Gets <see cref="IEventReader"/> for an event with the specified status byte.
        /// </summary>
        /// <param name="statusByte">Status byte to get reader for.</param>
        /// <param name="smfOnly">Indicates whether only reader for SMF events should be returned or not.</param>
        /// <returns>Reader for an event with the specified status byte.</returns>
        internal static IEventReader GetReader(byte statusByte, bool smfOnly)
        {
            if (statusByte == EventStatusBytes.Global.Meta)
                return MetaEventReader;

            return ChannelEventReader;
        }

        #endregion
    }
}
