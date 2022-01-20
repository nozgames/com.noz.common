using System;

namespace NoZ.Collections
{
    public class RingBuffer<T>
    {
        private T[] _elements;
        private int _head;
        private int _count;

        public int count => _count;

        public T this[int index]
        {
            get
            {
                if (index >= _count)
                    throw new IndexOutOfRangeException();

                return _elements[GetIndex(index)];
            }
        }

        public RingBuffer(int capacity)
        {
            _elements = new T[capacity];
        }

        public void PushBack (T value)
        {
            if (_count >= _elements.Length)
                throw new InsufficientMemoryException();

            _elements[GetIndex(_count)] = value;
            _count++;
        }

        public bool TryPushBack(T value)
        {
            if (_count >= _elements.Length)
                return false;

            PushBack(value);
            return true;
        }

        public T PopFront ()
        {
            if (_count == 0)
                throw new InvalidOperationException();

            var index = GetIndex(0);
            _head = GetIndex(1);
            _count--;
            return _elements[index];
        }

        public bool TryPopFront(out T value)
        {
            if (_count == 0)
            {
                value = default(T);
                return false;
            }

            value = PopFront();
            return true;
        }

        private int GetIndex(int offset) => (_head + offset) % _elements.Length;
    }
}

