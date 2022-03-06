using System;
using System.Collections;
using System.Collections.Generic;

public struct Optional<T> : IEnumerable<T>
{
    private readonly T _value;
    private readonly bool _hasValue;

    private Optional(T value, bool hasValue)
    {
        _value = value;
        _hasValue = hasValue;
    }

    public bool HasValue(out T container)
    {
        container = _value;
        return _hasValue;
    }

    public T GetValueUnsafe()
    {
        return _value;
    }

    public static Optional<T> FromSome(T value)
    {
        return new Optional<T>(value, true);
    }

    public static Optional<T> FromNone()
    {
        return new Optional<T>(default, false);
    }

    public Optional<T> OnSome(Action<T> callback)
    {
        if (_hasValue)
            callback?.Invoke(_value);

        return this;
    }

    public Optional<T> OnNone(Action callback)
    {
        if (!_hasValue)
            callback?.Invoke();

        return this;
    }

    public IEnumerator<T> GetEnumerator()
    {
        return new OptionalEnumerator(this);
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        return GetEnumerator();
    }

    public class OptionalEnumerator : IEnumerator<T>
    {
        T IEnumerator<T>.Current => _currentValue;
        object IEnumerator.Current => _currentValue;

        private readonly Optional<T> _target;
        private T _currentValue;
        private bool _isConsumed = false;

        public OptionalEnumerator(Optional<T> optional)
        {
            _target = optional;
        }

        public bool MoveNext()
        {
            if (_isConsumed)
                return false;

            _isConsumed = true;

            return _target.HasValue(out _currentValue);
        }

        public void Reset()
        {
            _isConsumed = false;
        }

        public void Dispose()
        {

        }
    }
}