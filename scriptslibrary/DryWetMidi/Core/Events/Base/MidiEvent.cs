using System;
using Melanchall.DryWetMidi.Common;

namespace Melanchall.DryWetMidi.Core
{
    /// <summary>
    /// Represents a MIDI file event stored in a track chunk.
    /// </summary>
    /// <remarks>
    /// <para>
    /// See <see href="https://www.midi.org/specifications/file-format-specifications/standard-midi-files"/> for detailed MIDI file specification.
    /// </para>
    /// </remarks>
    public abstract class MidiEvent
    {
        #region Constants

        /// <summary>
        /// Constant for content's size of events that don't have size information stored.
        /// </summary>
        public const int UnknownContentSize = -1;

        #endregion

        #region Fields

        internal long _deltaTime;

        #endregion

        #region Constructor

        /// <summary>
        /// Initializes a new instance of the <see cref="MidiEvent"/> with the specified event type.
        /// </summary>
        /// <param name="eventType">The type of event.</param>
        public MidiEvent(MidiEventType eventType)
        {
            EventType = eventType;
        }

        #endregion

        #region Properties

        /// <summary>
        /// Gets the type of the event.
        /// </summary>
        public MidiEventType EventType { get; }

        /// <summary>
        /// Gets or sets delta-time of the event.
        /// </summary>
        /// <remarks>
        /// Delta-time represents the amount of time before the following event. If the first event in a track
        /// occurs at the very beginning of a track, or if two events occur simultaneously, a delta-time of zero is used.
        /// Delta-time is in some fraction of a beat (or a second, for recording a track with SMPTE times), as specified
        /// by the file's time division.
        /// </remarks>
        /// <exception cref="ArgumentOutOfRangeException">Delta-time is negative.</exception>
        public long DeltaTime
        {
            get { return _deltaTime; }
            set
            {
                ThrowIfArgument.IsNegative(nameof(value), value, "Delta-time is negative.");

                _deltaTime = value;
            }
        }

        internal bool Flag { get; set; }

        #endregion

        #region Methods

        /// <summary>
        /// Reads content of a MIDI event.
        /// </summary>
        /// <param name="reader">Reader to read the content with.</param>
        /// <param name="settings">Settings according to which the event's content must be read.</param>
        /// <param name="size">Size of the event's content.</param>
        internal abstract void Read(MidiReader reader, ReadingSettings settings, int size);

        /// <summary>
        /// Clones event by creating a copy of it.
        /// </summary>
        /// <returns>Copy of the event.</returns>
        protected abstract MidiEvent CloneEvent();

        /// <summary>
        /// Clones event by creating a copy of it.
        /// </summary>
        /// <returns>Copy of the event.</returns>
        public MidiEvent Clone()
        {
            var midiEvent = CloneEvent();
            midiEvent._deltaTime = _deltaTime;
            return midiEvent;
        }

        #endregion
    }
}
