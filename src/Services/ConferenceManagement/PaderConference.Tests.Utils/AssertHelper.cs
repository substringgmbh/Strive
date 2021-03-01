﻿using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace PaderConference.Tests.Utils
{
    public static class AssertHelper
    {
        public static void AssertScrambledEquals<T>(IEnumerable<T> expected, IEnumerable<T> actual)
        {
            if (typeof(T).IsAssignableTo(typeof(IComparable)))
            {
                Assert.Equal(expected.OrderBy(x => x), actual.OrderBy(x => x));
            }
            else
            {
                var expectedList = expected.ToList();
                var actualList = actual.ToList();
                Assert.Equal(expectedList.Count, actualList.Count);

                foreach (var expectedItem in expectedList)
                {
                    Assert.Contains(expectedItem, actualList);
                }
            }
        }
    }
}