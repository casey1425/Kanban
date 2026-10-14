using Xunit;

// WeakReferenceMessenger.Default는 프로세스 전체에서 공유되므로, 테스트끼리 메시지가 섞이지 않게 순차 실행한다.
[assembly: CollectionBehavior(DisableTestParallelization = true)]
