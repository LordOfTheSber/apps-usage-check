using System.ComponentModel;

namespace AppsUsageCheck.App.ViewModels;

internal static class ProcessGridSorter
{
    private static readonly StringComparer NameComparer = StringComparer.OrdinalIgnoreCase;

    public static IReadOnlyList<ProcessItemViewModel> OrderItems(
        IEnumerable<ProcessItemViewModel> items,
        ProcessGridSortColumn sortColumn,
        ListSortDirection sortDirection)
    {
        ArgumentNullException.ThrowIfNull(items);

        return items
            .OrderBy(item => item, CreateComparer(sortColumn, sortDirection))
            .ToArray();
    }

    private static IComparer<ProcessItemViewModel> CreateComparer(
        ProcessGridSortColumn sortColumn,
        ListSortDirection sortDirection)
    {
        return Comparer<ProcessItemViewModel>.Create(
            (left, right) => ApplyDirection(CompareCore(left, right, sortColumn), sortDirection));
    }

    private static int CompareCore(
        ProcessItemViewModel? left,
        ProcessItemViewModel? right,
        ProcessGridSortColumn sortColumn)
    {
        if (ReferenceEquals(left, right))
        {
            return 0;
        }

        if (left is null)
        {
            return -1;
        }

        if (right is null)
        {
            return 1;
        }

        var result = sortColumn switch
        {
            ProcessGridSortColumn.Process => CompareProcess(left, right),
            ProcessGridSortColumn.State => CompareByValue(GetStateRank(left), GetStateRank(right), left, right),
            ProcessGridSortColumn.RunningTime => CompareByValue(left.DisplayedRunningSeconds, right.DisplayedRunningSeconds, left, right),
            ProcessGridSortColumn.ForegroundTime => CompareByValue(left.DisplayedForegroundSeconds, right.DisplayedForegroundSeconds, left, right),
            _ => throw new ArgumentOutOfRangeException(nameof(sortColumn), sortColumn, "Unsupported sort column."),
        };

        return result != 0 ? result : left.TrackedProcessId.CompareTo(right.TrackedProcessId);
    }

    private static int CompareProcess(ProcessItemViewModel left, ProcessItemViewModel right)
    {
        var result = NameComparer.Compare(left.PrimaryName, right.PrimaryName);
        if (result != 0)
        {
            return result;
        }

        return NameComparer.Compare(left.ProcessName, right.ProcessName);
    }

    private static int CompareByValue<T>(
        T leftValue,
        T rightValue,
        ProcessItemViewModel left,
        ProcessItemViewModel right)
        where T : IComparable<T>
    {
        var result = leftValue.CompareTo(rightValue);
        return result != 0 ? result : CompareProcess(left, right);
    }

    private static int GetStateRank(ProcessItemViewModel item)
    {
        if (item.IsForeground)
        {
            return 0;
        }

        if (item.IsRunning)
        {
            return 1;
        }

        return item.IsPaused ? 3 : 2;
    }

    private static int ApplyDirection(int value, ListSortDirection sortDirection)
    {
        return sortDirection == ListSortDirection.Descending ? -value : value;
    }
}
