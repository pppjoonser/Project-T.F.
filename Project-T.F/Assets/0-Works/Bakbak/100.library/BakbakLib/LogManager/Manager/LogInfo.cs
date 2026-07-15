using System;
using System.Collections.Generic;
using UnityEngine;

public struct LogInfo
{
    public string[] logTypes;
    public string message;

    // 추가 필드
    public string stackTrace;         // Unity의 stackTrace
    public UnityEngine.Object context;// Unity 로그의 context 오브젝트
    public DateTime timestamp;        // 정확한 시각(UTC)
    public int threadId;              // 발생 스레드 ID
    public string loggerName;         // 로그 카테고리/소스명
    public long sequenceId;           // 고유 순번/ID
    public Exception exception;       // 예외 객체(있을 경우)

    public LogInfo(
        string[] logType,
        string message,
        string stackTrace = null,
        UnityEngine.Object context = null,
        string loggerName = null,
        int threadId = 0,
        long sequenceId = 0,
        Exception exception = null,
        DateTime? timestamp = null)
    {
        this.logTypes = logType;
        this.message = message;
        this.stackTrace = stackTrace;
        this.context = context;
        this.loggerName = loggerName;
        this.threadId = threadId;
        this.sequenceId = sequenceId;
        this.exception = exception;
        this.timestamp = timestamp ?? DateTime.UtcNow;

        return;
    }
}
