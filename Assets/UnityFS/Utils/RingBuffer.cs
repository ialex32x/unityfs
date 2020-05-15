namespace UnityFS.Utils
{
    public class RingBuffer<T>
    {
        private int _read;
        private int _write;
        private T[] _list;

        public RingBuffer(int capacity)
        {
            _list = new T[capacity];
        }

        public bool IsFull()
        {
            return (_write + 1) % _list.Length == _read;
        }

        public bool IsEmpty()
        {
            return _read == _write;
        }

        public bool Enqueue(T value)
        {
            if (!IsFull())
            {
                _list[_write] = value;
                _write = (_write + 1) % _list.Length;
                return true;
            }

            return false;
        }

        public T Dequeue()
        {
            if (!IsEmpty())
            {
                var value = _list[_read];
                _list[_read] = default(T);
                _read = (_read + 1) % _list.Length;
                return value;
            }

            return default(T);
        }
    }
}