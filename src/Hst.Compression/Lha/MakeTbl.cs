/* ------------------------------------------------------------------------ */
/* LHa for UNIX                                                             */
/*              maketbl.c -- makes decoding table                           */
/*                                                                          */
/*      Modified                Nobutaka Watazaki                           */
/*                                                                          */
/*  Ver. 1.14   Source All chagned              1995.01.14  N.Watazaki      */
/* ------------------------------------------------------------------------ */
namespace Hst.Compression.Lha
{
    using System;

    public static class MakeTbl
    {
        public static void MakeTable(short nchar, byte[] bitlen, short tablebits, ushort[] table, ushort[] left,
            ushort[] right)
        {
            var count = new ushort[17];
            var weight = new ushort[17];
            var start = new ushort[17];

            int avail = nchar;

            /* initialize */
            for (var i = 1; i <= 16; i++)
            {
                count[i] = 0;
                weight[i] = (ushort)(1 << (16 - i));
            }

            /* count */
            for (var i = 0; i < nchar; i++)
            {
                if (bitlen[i] > 16)
                {
                    /* CVE-2006-4335 */
                    throw new Exception("Bad table (case a)");
                }
                else
                {
                    count[bitlen[i]]++;
                }
            }

            /* calculate first code */
            ushort total = 0;
            for (var i = 1; i <= 16; i++)
            {
                start[i] = (ushort)total;
                total += (ushort)(weight[i] * count[i]);
            }

            if ((total & 0xffff) != 0 || tablebits > 16)
            {
                /* 16 for weight below */
                throw new Exception($"make_table(): Bad table (case b), total = {total}, tablebits = {tablebits}");
            }

            /* shift data for make table. */
            var m = 16 - tablebits;
            for (var i = 1; i <= tablebits; i++)
            {
                start[i] >>= m;
                weight[i] >>= m;
            }

            /* initialize */
            var j = start[tablebits + 1] >> m;
            var k = Math.Min(1 << tablebits, 4096);
            if (j != 0)
                for (var i = j; i < k; i++)
                    table[i] = 0;

            /* create table and tree */
            for (j = 0; j < nchar; j++)
            {
                k = bitlen[j];
                if (k == 0)
                    continue;
                var l = (uint)(start[k] + weight[k]);
                if (k <= tablebits)
                {
                    /* code in table */
                    l = Math.Min(l, 4096);
                    for (var i = start[k]; i < l; i++)
                        table[i] = (ushort)j;
                }
                else
                {
                    /* code not in table */
                    uint i = start[k];
                    if (i >> m > 4096)
                    {
                        /* CVE-2006-4337 */
                        throw new Exception("Bad table (case c)");
                    }

                    // unsigned short *p;
                    // p = &table[i >> m];
                    // note: set p reference to element (i >> m) in table array
                    var pArray = table; // set pointer array to table
                    var pRef = i >> m; // set pointer reference to element i >> m in array
                    
                    i <<= tablebits;
                    var n = k - tablebits;
                    /* make tree (n length) */
                    while (--n >= 0)
                    {
                        // if (*p == 0) {
                        if (pArray[pRef] == 0) 
                        {
                            right[avail] = left[avail] = 0;
                            // *p = avail++;
                            // note: set value of pointer to avail + 1
                            pArray[pRef] = (ushort)(avail++);
                        }
                        
                        if ((i & 0x8000) != 0)
                        {
                            // p = &right[*p];
                            // note 1: "*p" get value of pointer p
                            // note 2: "&right" get reference (address) to element (value of pointer p) in table right
                            // note 3: "p =" set pointer to reference
                            pRef = pArray[pRef]; // set pointer reference to pointer array at position pointer reference
                            pArray = right; // set pointer array to right
                        }
                        else
                        {
                            // p = &left[*p];
                            // note 1: "*p" get value of pointer p
                            // note 2: "&left" get reference (address) to element (value of pointer p) in table left
                            // note 3: "p =" set pointer to reference
                            pRef = pArray[pRef]; // set pointer reference to pointer array at position pointer reference
                            pArray = left; // set pointer array to left
                        }
                        i <<= 1;
                    }

                    //*p = j;
                    // note: set value of pointer to j
                    pArray[pRef] = (ushort)j;
                }

                start[k] = (ushort)l;
            }
        }
    }    
}