﻿namespace Chat2Report.Models
{
    using System;
    using System.Collections.Generic;
    using System.Text;
    using System.Threading.Tasks;


    /// <summary>
    /// Претставува резултат кој може да се конзумира како асинхрон стрим од стрингови,
    /// но исто така може и да се чека за да го даде целиот акумулиран (материјализиран) резултат.
    /// Ова овозможува еден излез да се користи и за прикажување во реално време и за понатамошна
    /// секвенцијална обработка.
    /// </summary>
    public class StreamableResult
    {
        private readonly IAsyncEnumerable<string> _sourceStream;
        private readonly Lazy<Task<string>> _materializedResult;

        public StreamableResult(IAsyncEnumerable<string> sourceStream)
        {
            _sourceStream = sourceStream ?? throw new ArgumentNullException(nameof(sourceStream));
            _materializedResult = new Lazy<Task<string>>(async () =>
            {
                var sb = new StringBuilder();
                await foreach (var chunk in _sourceStream)
                {
                    sb.Append(chunk);
                }
                return sb.ToString();
            });
        }

        /// <summary>
        /// Го враќа оригиналниот IAsyncEnumerable стрим за конзумирање дел по дел.
        /// </summary>
        public IAsyncEnumerable<string> GetStream() => _sourceStream;

        /// <summary>
        /// Го материјализира стримот (ако веќе не е) и го враќа целиот резултат како еден string.
        /// Овој резултат се кешира, па следните повици се инстант.
        /// </summary>
        /// <returns>Task кој резултира со целосниот текстуален резултат.</returns>
        public Task<string> GetAwaitableResult()
        {
            return _materializedResult.Value;
        }
    }
}
