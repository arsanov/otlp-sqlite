using System.Collections.Generic;
using System.Reactive.Subjects;

namespace OtlpServer.Extensions
{
    public static class SubjectExtension
    {
        public static void OnNextRange<T>(this ISubject<T> self, IEnumerable<T> items)
        {
            foreach (var item in items)
            {
                self.OnNext(item);
            }
        }
    }
}
