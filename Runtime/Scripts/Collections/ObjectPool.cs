﻿/*
  NoZ Unity Library

  Copyright(c) 2019 NoZ Games, LLC

  Permission is hereby granted, free of charge, to any person obtaining a copy
  of this software and associated documentation files(the "Software"), to deal
  in the Software without restriction, including without limitation the rights
  to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
  copies of the Software, and to permit persons to whom the Software is
  furnished to do so, subject to the following conditions :

  The above copyright notice and this permission notice shall be included in all
  copies or substantial portions of the Software.

  THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
  IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
  FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT.IN NO EVENT SHALL THE
  AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
  LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
  OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
  SOFTWARE.
*/

using System.Collections.Generic;

namespace NoZ
{
    public class ObjectPool<T> where T : class
    {
        public delegate T Allocator();

        private readonly Stack<T> _items;
        private readonly int _capacity;
        private readonly Allocator _allocator;

        public ObjectPool(Allocator allocator, int capacity)
        {
            _allocator = allocator;
            _capacity = capacity;
            _items = new Stack<T>(capacity);
        }

        public T Get() => _items.Count == 0 ? _allocator() : _items.Pop();

        public void Release(T t)
        {
            if (_items.Count == _capacity)
                return;
            
            _items.Push(t);
        }
    }
}