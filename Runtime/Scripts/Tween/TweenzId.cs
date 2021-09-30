/*
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

namespace NoZ.Tweenz
{
    public struct TweenzId
    {
        public static readonly TweenzId Empty = new TweenzId { _index = 0, _iteration = 0 };

        internal uint _index;
        internal uint _iteration;

        public bool Equals(TweenzId other) => other._index == _index && other._iteration == _iteration;

        public override string ToString() => ((ulong)this).ToString();

        public static bool operator ==(TweenzId lhs, TweenzId rhs) => lhs._index == rhs._index && lhs._iteration == rhs._iteration;

        public static bool operator !=(TweenzId lhs, TweenzId rhs) => !(lhs == rhs);

        public override bool Equals(object obj) => this.Equals((TweenzId)obj);

        public static implicit operator ulong(TweenzId id) => (((ulong)id._index) << 32) + id._iteration;

        public override int GetHashCode() => (int)_index;
    }
}