/*
 * dk.brics.automaton
 * 
 * Copyright (c) 2001-2011 Anders Moeller
 * All rights reserved.
 * 
 * Redistribution and use in source and binary forms, with or without
 * modification, are permitted provided that the following conditions
 * are met:
 * 1. Redistributions of source code must retain the above copyright
 *    notice, this list of conditions and the following disclaimer.
 * 2. Redistributions in binary form must reproduce the above copyright
 *    notice, this list of conditions and the following disclaimer in the
 *    documentation and/or other materials provided with the distribution.
 * 3. The name of the author may not be used to endorse or promote products
 *    derived from this software without specific prior written permission.
 * 
 * THIS SOFTWARE IS PROVIDED BY THE AUTHOR ``AS IS'' AND ANY EXPRESS OR
 * IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE IMPLIED WARRANTIES
 * OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE DISCLAIMED.
 * IN NO EVENT SHALL THE AUTHOR BE LIABLE FOR ANY DIRECT, INDIRECT,
 * INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES (INCLUDING, BUT
 * NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES; LOSS OF USE,
 * DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND ON ANY
 * THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT
 * (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF
 * THIS SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
 */

using System.Collections.Generic;

namespace NAutomaton
{
    public static class MinimizationOperations
    {
        /// <summary>
        /// Minimizes (and determinizes if not already deterministic) the given automaton.
        /// </summary>
        /// <param name="a">The automaton.</param>
        public static void Minimize(Automaton a)
        {
            if (!a.IsSingleton)
            {
                switch (Automaton.Minimization)
                {
                    case Automaton.MinimizeHuffman:
                        MinimizationOperations.MinimizeHuffman(a);
                        break;
                    case Automaton.MinimizeBrzozowski:
                        MinimizationOperations.MinimizeBrzozowski(a);
                        break;
                    default:
                        MinimizationOperations.MinimizeHopcroft(a);
                        break;
                }
            }

            a.RecomputeHashCode();
        }

        public static void MinimizeHopcroft(Automaton a)
        {
            a.Determinize();
            IList<Transition> tr = a.Initial.Transitions;
            if (tr.Count == 1) 
            {
	            Transition t = tr[0];
	            if (t.To == a.Initial && t.Min == char.MinValue && t.Max == char.MaxValue)
		            return;
            }
            a.Totalize();
            // make arrays for numbered states and effective alphabet
            HashSet<State> ss = a.GetStates();
            var states = new State[ss.Count];
            int number = 0;
            foreach (State q in ss) {
	            states[number] = q;
	            q.Number = number++;
            }
            char[] sigma = a.GetStartPoints();
            // Initialize data structures
            List<List<LinkedList<State>>> reverse = new List<List<LinkedList<State>>>();
            for (int q = 0; q < states.Length; q++) {
	            List<LinkedList<State>> v = new List<LinkedList<State>>();
	            Initialize(v, sigma.Length);
	            reverse.Add(v);
            }
            bool[][] reverse_nonempty = new bool[states.Length][sigma.Length];
            List<LinkedList<State>> partition = new List<LinkedList<State>>();
            Initialize(partition, states.Length);
            int[] block = new int[states.Length];
            StateList[][] active = new StateList[states.Length][sigma.Length];
            StateListNode[][] active2 = new StateListNode[states.Length][sigma.Length];
            LinkedList<IntPair> pending = new LinkedList<IntPair>();
            bool[][] pending2 = new bool[sigma.Length][states.Length];
            List<State> split = new List<State>();
            bool[] split2 = new bool[states.Length];
            List<int> refine = new List<int>();
            bool[] refine2 = new bool[states.Length];
            List<List<State>> splitblock = new List<List<State>>();
            Initialize(splitblock, states.Length);
            for (int q = 0; q < states.Length; q++) {
	            splitblock.set(q, new List<State>());
	            partition.set(q, new LinkedList<State>());
	            for (int x = 0; x < sigma.Length; x++) {
		            reverse.get(q).set(x, new LinkedList<State>());
		            active[q][x] = new StateList();
	            }
            }
            // find initial partition and reverse edges
            for (int q = 0; q < states.Length; q++) {
	            State qq = states[q];
	            int j;
	            if (qq.Accept)
		            j = 0;
	            else
		            j = 1;
	            partition.get(j).add(qq);
	            block[qq.Number] = j;
	            for (int x = 0; x < sigma.Length; x++) {
		            char y = sigma[x];
		            State p = qq.Step(y);
		            reverse.get(p.Number).get(x).add(qq);
		            reverse_nonempty[p.Number][x] = true;
	            }
            }
            // Initialize active sets
            for (int j = 0; j <= 1; j++)
	            for (int x = 0; x < sigma.Length; x++)
		            foreach(State qq in partition.get(j))
			            if (reverse_nonempty[qq.Number][x])
				            active2[qq.Number][x] = active[j][x].add(qq);
            // Initialize pending
            for (int x = 0; x < sigma.Length; x++) {
	            int a0 = active[0][x].Size;
	            int a1 = active[1][x].Size;
	            int j;
	            if (a0 <= a1)
		            j = 0;
	            else
		            j = 1;
	            pending.add(new IntPair(j, x));
	            pending2[x][j] = true;
            }
            // process pending until fixed point
            int k = 2;
            while (!pending.isEmpty()) {
	            IntPair ip = pending.RemoveAndReturnFirst();
	            int p = ip.N1;
	            int x = ip.N2;
	            pending2[x][p] = false;
	            // find states that need to be split off their blocks
	            for (StateListNode m = active[p][x].First; m != null; m = m.Next)
		            foreach (State s in reverse.get(m.State.Number).get(x))
			            if (!split2[s.Number]) {
				            split2[s.Number] = true;
				            split.Add(s);
				            int j = block[s.Number];
				            splitblock.get(j).add(s);
				            if (!refine2[j]) {
					            refine2[j] = true;
					            refine.Add(j);
				            }
			            }
	            // refine blocks
	            foreach (int j in refine) {
		            if (splitblock.get(j).size() < partition.get(j).size()) {
			            LinkedList<State> b1 = partition.get(j);
			            LinkedList<State> b2 = partition.get(k);
			            foreach (State s in splitblock.get(j)) {
				            b1.remove(s);
				            b2.add(s);
				            block[s.Number] = k;
				            for (int c = 0; c < sigma.Length; c++) {
					            StateListNode sn = active2[s.Number][c];
					            if (sn != null && sn.sl == active[j][c]) {
						            sn.remove();
						            active2[s.Number][c] = active[k][c].add(s);
					            }
				            }
			            }
			            // update pending
			            for (int c = 0; c < sigma.Length; c++) {
				            int aj = active[j][c].size;
				            int ak = active[k][c].size;
				            if (!pending2[c][j] && 0 < aj && aj <= ak) {
					            pending2[c][j] = true;
					            pending.add(new IntPair(j, c));
				            } else {
					            pending2[c][k] = true;
					            pending.add(new IntPair(k, c));
				            }
			            }
			            k++;
		            }
		            foreach (State s in splitblock.get(j))
			            split2[s.Number] = false;
		            refine2[j] = false;
		            splitblock.get(j).clear();
	            }
	            split.Clear();
	            refine.Clear();
            }
            // make a new state for each equivalence class, set initial state
            State[] newstates = new State[k];
            for (int n = 0; n < newstates.Length; n++) {
	            State s = new State();
	            newstates[n] = s;
	            foreach (State q in partition.get(n)) {
		            if (q == a.Initial)
			            a.Initial = s;
		            s.Accept = q.Accept;
		            s.Number = q.Number; // select representative
		            q.Number = n;
	            }
            }
            // build transitions and set acceptance
            for (int n = 0; n < newstates.Length; n++) {
	            State s = newstates[n];
	            s.Accept = states[s.Number].Accept;
	            foreach (Transition t in states[s.Number].Transitions)
		            s.Transitions.Add(new Transition(t.Min, t.Max, newstates[t.To.Number]));
            }
            a.RemoveDeadTransitions();
        }

        private static void Initialize<T>(List<T> list, int size)
        {
            list = new List<T>(size);
        }

        public static void MinimizeBrzozowski(Automaton a)
        {
            throw new System.NotImplementedException();
        }

        public static void MinimizeHuffman(Automaton a)
        {
            throw new System.NotImplementedException();
        }

        #region Nested type: IntPair

        private sealed class IntPair
        {
            private readonly int n1;
            private readonly int n2;

            public IntPair(int n1, int n2)
            {
                this.n1 = n1;
                this.n2 = n2;
            }

            public int N1
            {
                get { return n1; }
            }

            public int N2
            {
                get { return n2; }
            }
        }

        #endregion

        #region Nested type: StateList

        private sealed class StateList
        {
            public int Size { get; set; }

            public StateListNode First { get; set; }

            public StateListNode Last { get; set; }

            public StateListNode Add(State q)
            {
                return new StateListNode(q, this);
            }
        }

        #endregion

        #region Nested type: StateListNode

        private sealed class StateListNode
        {
            public StateListNode(State q, StateList sl)
            {
                State = q;
                StateList = sl;
                if (sl.Size++ == 0)
                {
                    sl.First = sl.Last = this;
                }
                else
                {
                    sl.Last.Next = this;
                    Prev = sl.Last;
                    sl.Last = this;
                }
            }

            public StateListNode Next { get; private set; }

            private StateListNode Prev { get; set; }

            public StateList StateList { get; private set; }

            public State State { get; private set; }

            public void Remove()
            {
                StateList.Size--;
                if (StateList.First == this)
                {
                    StateList.First = Next;
                }
                else
                {
                    Prev.Next = Next;
                }

                if (StateList.Last == this)
                {
                    StateList.Last = Prev;
                }
                else
                {
                    Next.Prev = Prev;
                }
            }
        }

        #endregion
    }
}