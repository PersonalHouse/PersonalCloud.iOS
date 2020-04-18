// This file is used by Code Analysis to maintain SuppressMessage
// attributes that are applied to this project.
// Project-level suppressions either have no target or are given
// a specific target and scoped to a namespace, type, member, etc.

using System.Diagnostics.CodeAnalysis;

[assembly: SuppressMessage("Design", "CA1062", Justification = "Callbacks do not need to null-check their parameters.", Scope = "module")]
[assembly: SuppressMessage("Design", "CA1031", Justification = "Catch general exceptions to prevent UI from crashing.", Scope = "module")]
[assembly: SuppressMessage("Reliability", "CA2000", Justification = "Xamarin objects are disposables, but disposal is handled by runtimes.", Scope = "module")]
[assembly: SuppressMessage("Design", "CA1010", Justification = "Supress warnings caused by Xamarin types.", Scope = "module")]
[assembly: SuppressMessage("Naming", "CA1710", Justification = "Supress warnings caused by Xamarin types.", Scope = "module")]
[assembly: SuppressMessage("Performance", "CA1822", Justification = "Supress warnings caused by Xamarin types.", Scope = "module")]
