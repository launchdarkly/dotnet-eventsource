using System;

namespace LaunchDarkly.EventSource.Internal
{
	/// <summary>
	/// Simple concurrency helper for a property that should always be read or
	/// written under lock. In some cases we can simply use a volatile field
	/// instead, but volatile can't be used for some types.
	/// </summary>
	/// <typeparam name="T">the value type</typeparam>
	internal sealed class ValueWithLock<T>
	{
		private readonly object _lockObject;
		private T _value;

		public ValueWithLock(object lockObject, T initialValue)
		{
			_lockObject = lockObject;
			_value = initialValue;
		}

		public T Get()
		{
			lock (_lockObject) { return _value; }
		}

        public void Set(T value)
        {
            lock (_lockObject) { _value = value; }
        }

		public T GetAndSet(T newValue)
		{
			lock (_lockObject)
			{
				var oldValue = _value;
				_value = newValue;
				return oldValue;
			}
		}
    }
}

