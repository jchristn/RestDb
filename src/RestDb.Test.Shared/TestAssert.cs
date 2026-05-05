namespace RestDb.Test.Shared;

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

internal static class TestAssert
{
    public static void True(bool condition, string? message = null)
    {
        if (!condition) throw new InvalidOperationException(message ?? "Expected condition to be true.");
    }

    public static void False(bool condition, string? message = null)
    {
        if (condition) throw new InvalidOperationException(message ?? "Expected condition to be false.");
    }

    public static void NotNull(object? value, string? message = null)
    {
        if (value == null) throw new InvalidOperationException(message ?? "Expected value to be non-null.");
    }

    public static void Null(object? value, string? message = null)
    {
        if (value != null) throw new InvalidOperationException(message ?? "Expected value to be null.");
    }

    public static void Equal<T>(T expected, T actual, string? message = null)
    {
        if (!EqualityComparer<T>.Default.Equals(expected, actual))
        {
            throw new InvalidOperationException(message ?? ("Expected '" + expected + "' but found '" + actual + "'."));
        }
    }

    public static void Contains(string expectedSubstring, string? actual, StringComparison comparison, string? message = null)
    {
        if (string.IsNullOrEmpty(actual) || actual.IndexOf(expectedSubstring, comparison) < 0)
        {
            throw new InvalidOperationException(message ?? ("Expected string to contain '" + expectedSubstring + "'."));
        }
    }

    public static void DoesNotContain(string unexpectedSubstring, string? actual, StringComparison comparison, string? message = null)
    {
        if (!string.IsNullOrEmpty(actual) && actual.IndexOf(unexpectedSubstring, comparison) >= 0)
        {
            throw new InvalidOperationException(message ?? ("Expected string not to contain '" + unexpectedSubstring + "'."));
        }
    }

    public static void Contains<T>(IEnumerable<T> collection, Func<T, bool> predicate, string? message = null)
    {
        if (collection == null || !collection.Any(predicate))
        {
            throw new InvalidOperationException(message ?? "Expected collection to contain a matching element.");
        }
    }

    public static void DoesNotContain<T>(IEnumerable<T> collection, Func<T, bool> predicate, string? message = null)
    {
        if (collection != null && collection.Any(predicate))
        {
            throw new InvalidOperationException(message ?? "Expected collection not to contain a matching element.");
        }
    }

    public static void Single(IEnumerable collection, string? message = null)
    {
        int count = Count(collection);
        if (count != 1)
        {
            throw new InvalidOperationException(message ?? ("Expected exactly one element but found " + count + "."));
        }
    }

    public static void Empty(IEnumerable collection, string? message = null)
    {
        int count = Count(collection);
        if (count != 0)
        {
            throw new InvalidOperationException(message ?? ("Expected collection to be empty but found " + count + " element(s)."));
        }
    }

    public static T IsType<T>(object? value, string? message = null)
    {
        if (value is not T typed)
        {
            throw new InvalidOperationException(message ?? ("Expected value of type " + typeof(T).Name + "."));
        }

        return typed;
    }

    private static int Count(IEnumerable collection)
    {
        if (collection == null) return 0;

        int count = 0;
        foreach (object? _ in collection)
        {
            count++;
        }

        return count;
    }
}
