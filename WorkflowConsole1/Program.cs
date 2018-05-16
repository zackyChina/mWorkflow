using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

/// <summary>
/// lightweight workflow for simple task
/// 张晨旭 2018.4.26
/// </summary>
namespace WorkflowConsole1
{
    class Program
    {
        static void Main(string[] args)
        {
            WorkflowEngine workflowEngine = new WorkflowEngine();

            //定义工作流活动1
            WorkflowActivity file1Activity = new WorkflowActivity("ACT_20180504", "代码检查流程_文件A");
            BaseTransitionAdapter transitionAdapter = new CodeCheckTransitionAdapter();
            file1Activity.AddTransitionAdapter(transitionAdapter);
            //定义工作流活动2
            WorkflowActivity file2Activity = new WorkflowActivity("ACT_20180506", "代码检查流程_文件Y");
            BaseTransitionAdapter transitionAdapter3 = new CodeCheckTransitionAdapter();
            file2Activity.AddTransitionAdapter(transitionAdapter3);

            //引擎开始
            workflowEngine.Start();

            //模拟一段时间后开始流程操作
            Thread.Sleep(5000);      
            workflowEngine.AddAcivity(file1Activity);   //活动1 放入工作流引擎开始处理
            file1Activity.CreateWorksheet();

            Thread.Sleep(8000);
            file1Activity.ApproveNode();    //容许活动1当前节点
            workflowEngine.AddAcivity(file2Activity);  //活动2 放入工作流引擎开始处理
            file2Activity.CreateWorksheet();

            //模拟一段时间后拒绝当前流程
            Thread.Sleep(8000);
            file2Activity.CancelNode();

            Thread.Sleep(12000);
            Console.WriteLine("----------------------------------------------------------------------");
            file1Activity.ApproveNode();     //容许活动1当前节点
            Console.ReadLine();
            workflowEngine.Stop();
        }
    }

    public class WorkflowEngine : IDisposable
    {
        List<WorkflowActivity> _workflowPendingList = new List<WorkflowActivity>();
        Queue<WorkflowActivity> _workflowQueue = new Queue<WorkflowActivity>();
        Queue<WorkflowActivity> _workflowQueueHistory = new Queue<WorkflowActivity>();
        Thread _thread;
        bool EngineStop = false;

        public WorkflowEngine()
        {
            _thread = new Thread(new ThreadStart(WorkflowThread));
            _thread.Priority = ThreadPriority.BelowNormal;
        }

        public void Start()
        {
            Console.WriteLine(">> workflow engine start");
            _thread.Start();
        }

        public void Stop()
        {
            Console.WriteLine(">> workflow engine stop");
            EngineStop = true;
            _thread.Abort();
        }

        public void WorkflowThread()
        {
            for (; ; )
            {
                if (EngineStop)
                    return;

                Console.WriteLine(">> engine pending list items: " + _workflowPendingList.Count);
                Console.WriteLine(">> engineQueue items: " + _workflowQueue.Count);
                lock (_workflowPendingList)
                {
                    if (_workflowQueue.Count == 0)
                    {
                        foreach (var activity in _workflowPendingList)
                        {
                            Console.WriteLine("-----[ acivity: " + activity.WorkFlowName + " ----- " + activity);
                            if (activity.CurrentActivityStatus == ACTIVITY_STATUS.Started)
                            {
                                _workflowQueue.Enqueue(activity);
                            }
                        }
                    }
                }

                if (_workflowQueue.Count > 0)
                {
                    lock (_workflowQueue)
                    {
                        Console.WriteLine(">> handle workflow...");
                        WorkflowActivity acivity = _workflowQueue.Dequeue();
                        //处理工作流
                        HandleWorkflow(acivity);
                    }
                }
                else
                {
                    Console.WriteLine(">> engineQueue items: " + _workflowQueue.Count);
                    Thread.Sleep(5000);
                }
            }
        }

        private void HandleWorkflow(WorkflowActivity activity)
        {
            Console.WriteLine(">>>>> deal with acivity:" + activity.WorkFlowName);
            var currentNode = activity.CurrentNode;
            //Console.WriteLine(">>>>> acivity:" + activity.WorkFlowName + "  status: " + activity.CurrentActivityStatus + " workksheet: " + activity.CurrentWorksheet);
            activity.Process();
            Console.WriteLine(">>>>> acivity:" + activity.WorkFlowName + "  status: " + activity.CurrentActivityStatus + " workksheet: " + activity.CurrentWorksheet);
            Thread.Sleep(4000);
        }

        public void AddAcivity(WorkflowActivity activity)
        {
            _workflowPendingList.Add(activity);
        }

        public void Dispose()
        {
            _thread.Abort();

        }
    }

    public class CodeCheckTransitionAdapter : BaseTransitionAdapter
    {
        private Dictionary<int, TransitionState> _transitionMap = new Dictionary<int, TransitionState>();
        public CodeCheckTransitionAdapter() { }

        public override void CreateTransitionMap()
        {
            _transitionMap.Add(1, new TransitionState { CurrentState = CODE_CHECK_NODE.CC_WAIT.ToString(), NextState = CODE_CHECK_NODE.CC_COMMIT.ToString() });
            _transitionMap.Add(2, new TransitionState { CurrentState = CODE_CHECK_NODE.CC_COMMIT.ToString(), NextState = CODE_CHECK_NODE.CC_R_CHECK.ToString() });
            _transitionMap.Add(3, new TransitionState { CurrentState = CODE_CHECK_NODE.CC_R_CHECK.ToString(), NextState = CODE_CHECK_NODE.CC_F_CHECK.ToString() });
            _transitionMap.Add(4, new TransitionState { CurrentState = CODE_CHECK_NODE.CC_F_CHECK.ToString(), NextState = CODE_CHECK_NODE.CC_MERGE.ToString() });
        }

        public override Dictionary<int, TransitionState> GetMap()
        {
            return _transitionMap;
        }

        public override CODE_CHECK_NODE GetNextNode(CODE_CHECK_NODE nodeDef)
        {
            lock (_transitionMap)
            {
                foreach (var item in _transitionMap)
                {
                    var state = item.Value;
                    if (state.CurrentState.ToUpper() == nodeDef.ToString().ToUpper())
                    {
                        return (CODE_CHECK_NODE)Enum.Parse(typeof(CODE_CHECK_NODE), state.NextState);
                    }
                }
            }
            return CODE_CHECK_NODE.NONE;
        }
    }

    public class WorkflowActivity : IEquatable<WorkflowActivity>
    {
        public WorkflowActivity(string acivityID, string workFlowName)
        {
            WorkFlowName = workFlowName;
            CurrentNode = CODE_CHECK_NODE.CC_WAIT;
            CurrentActivityStatus = ACTIVITY_STATUS.Pending;
            ActivityID = acivityID;
        }

        public string WorkFlowName { get; set; }
        private BaseTransitionAdapter _transitionAdapter;
        public CODE_CHECK_NODE CurrentNode { get; set; }
        public CODE_CHECK_NODE NextNode { get; set; }
        public ACTIVITY_STATUS CurrentActivityStatus { get; private set; }
        public Node CurrentWorksheet { get; private set; }

        private List<Node> worksheetsHistory = new List<Node>();

        public string ActivityID { get; set; }  //unique identifier

        public void AddTransitionAdapter(BaseTransitionAdapter adapter)
        {
            _transitionAdapter = adapter;
        }

        public override string ToString()
        {
            StringBuilder sb = new StringBuilder();
            foreach (var item in worksheetsHistory)
            {
                sb.AppendLine("    +++" + item);
            }
            sb.AppendLine("    +++" + CurrentWorksheet);

            return sb.ToString();
        }

        public void ApproveNode()
        {
            lock (CurrentWorksheet)
            {
                if (CurrentWorksheet != null && CurrentActivityStatus == ACTIVITY_STATUS.Started)
                {
                    Console.WriteLine(">>>>> acivity:" + WorkFlowName + " NODE APPROVED!!!");
                    if (SetWorksheetStatus(NODE_ACTION_STATUS.Approved))
                        Console.WriteLine(">>>>> acivity:" + WorkFlowName + " NODE APPROVED set successful !!!");
                    else
                        Console.WriteLine(">>>>> acivity:" + WorkFlowName + " NODE APPROVED set failed !!!");
                }
            }
        }
        public void CancelNode()
        {
            lock (CurrentWorksheet)
            {
                if (CurrentWorksheet != null && CurrentActivityStatus == ACTIVITY_STATUS.Started)
                {
                    Console.WriteLine(">>>>> acivity:" + WorkFlowName + " NODE REJECTED!!!");
                    if (SetWorksheetStatus(NODE_ACTION_STATUS.Cancelled))
                        Console.WriteLine(">>>>> acivity:" + WorkFlowName + " NODE CANCEL set successful !!!");
                    else
                        Console.WriteLine(">>>>> acivity:" + WorkFlowName + " NODE CANCEL set failed !!!");
                }
            }
        }

        public void Process()
        {
            lock (CurrentWorksheet)
            {
                if (CurrentWorksheet == null)
                {
                    Console.WriteLine("**************** acivity:" + WorkFlowName + " process doing nothing !!!***************");
                    return;
                }
                if (CurrentActivityStatus == ACTIVITY_STATUS.Finished || CurrentActivityStatus == ACTIVITY_STATUS.Cancel)
                {
                    Console.WriteLine(">>>>> acivity:" + WorkFlowName + " status is cancel or finished, no process");
                    return;
                }
                if (CurrentWorksheet.CurrentStatus == NODE_ACTION_STATUS.Assigned)
                {
                    SetStatus(ACTIVITY_STATUS.Started);
                }
                if (CurrentActivityStatus == ACTIVITY_STATUS.Started)
                {
                    Console.WriteLine(">>>>> acivity:" + WorkFlowName + " started, process...");
                    if (CanTransiteNextNode())
                    {
                        CurrentNode = NextNode;
                        Console.WriteLine(">>>>> acivity:" + WorkFlowName + " transite to next node: " + CurrentNode.ToString());
                        NextNode = CODE_CHECK_NODE.NONE;
                        Console.WriteLine(">>>>> acivity:" + WorkFlowName + " --WORKSHEET changed !!! ");
                        CurrentWorksheet.CurrentNode = CurrentNode;
                        CurrentWorksheet.CurrentStatus = NODE_ACTION_STATUS.Assigned;
                    }

                    switch (CurrentWorksheet.CurrentStatus)
                    {
                        case NODE_ACTION_STATUS.Reject:
                        case NODE_ACTION_STATUS.Deny:
                        case NODE_ACTION_STATUS.Cancelled:
                            if (SetWorksheetStatus(NODE_ACTION_STATUS.Cancelled))
                            {
                                SetStatus(ACTIVITY_STATUS.Cancel);
                                worksheetsHistory.Add((Node)CurrentWorksheet.Clone());
                                CurrentWorksheet = null;
                            }
                            break;
                    }
                }
            }
        }

        public bool SetWorksheetStatus(NODE_ACTION_STATUS status)
        {
            if (CurrentActivityStatus != ACTIVITY_STATUS.Started)
                return false;

            if (CurrentWorksheet != null)
            {
                lock (CurrentWorksheet)
                {
                    switch (status)
                    {
                        case NODE_ACTION_STATUS.Assigned:
                            CurrentWorksheet.CurrentStatus = NODE_ACTION_STATUS.Assigned;
                            break;
                        case NODE_ACTION_STATUS.Approved:
                            if (CurrentWorksheet.CurrentStatus == NODE_ACTION_STATUS.Assigned)
                            {
                                CurrentWorksheet.CurrentStatus = NODE_ACTION_STATUS.Approved;
                                return true;
                            }
                            else
                                return false;
                        case NODE_ACTION_STATUS.Deny:
                        case NODE_ACTION_STATUS.Reject:
                        case NODE_ACTION_STATUS.Cancelled:
                            CurrentWorksheet.CurrentStatus = NODE_ACTION_STATUS.Cancelled;
                            return true;
                    }
                }
            }
            return false;
        }

        public bool CanTransiteNextNode()
        {
            if (CurrentWorksheet != null)
            {
                lock (CurrentWorksheet)
                {
                    if (CurrentWorksheet.CurrentStatus == NODE_ACTION_STATUS.Approved)
                    {
                        NextNode = _transitionAdapter.GetNextNode(CurrentWorksheet.CurrentNode);
                        Console.WriteLine(">>>>> acivity:" + WorkFlowName + " transite to next node: " + NextNode.ToString());
                        return true;
                    }
                }
            }
            return false;
        }

        public bool SetStatus(ACTIVITY_STATUS status)
        {
            lock (this)
            {
                switch (status)
                {
                    case ACTIVITY_STATUS.Started:
                        if (CurrentActivityStatus == ACTIVITY_STATUS.Pending && worksheetsHistory.Any() == false)
                        {
                            CurrentActivityStatus = ACTIVITY_STATUS.Started;
                            Console.WriteLine("workflow activity set status: " + CurrentActivityStatus);
                        }
                        break;
                    case ACTIVITY_STATUS.Cancel:
                        if (CurrentActivityStatus == ACTIVITY_STATUS.Started)
                        {
                            CurrentActivityStatus = ACTIVITY_STATUS.Cancel;
                            Console.WriteLine("workflow activity set status: " + CurrentActivityStatus);
                        }
                        break;
                    case ACTIVITY_STATUS.Finished:
                        if (CurrentActivityStatus == ACTIVITY_STATUS.Started)
                        {
                            CurrentActivityStatus = ACTIVITY_STATUS.Finished;
                            Console.WriteLine("workflow activity set status: " + CurrentActivityStatus);
                        }
                        break;
                }
            }
            return true;
        }

        public void CreateWorksheet()
        {
            lock (this)
            {
                Node worksheet = new Node();
                worksheet.CurrentStatus = NODE_ACTION_STATUS.Assigned;
                worksheet.CurrentNode = CurrentNode;
                worksheet.CreateDate = DateTime.Now;

                SetStatus(ACTIVITY_STATUS.Started);
                CurrentWorksheet = worksheet;
            }
        }

        public bool Equals(WorkflowActivity other)
        {
            if (this.Equals(other) && this.ActivityID.Equals(other.ActivityID))
                return true;
            return false;
        }
    }

    public abstract class BaseTransitionAdapter
    {
        public BaseTransitionAdapter()
        {
            CreateTransitionMap();
        }
        public abstract void CreateTransitionMap();
        public abstract CODE_CHECK_NODE GetNextNode(CODE_CHECK_NODE nodeDef);
        public abstract Dictionary<int, TransitionState> GetMap();
    }

    public enum ACTIVITY_STATUS
    {
        Pending, //   未启动流程
        Started,  //   已启动流程
        Finished,   //   已结束
        Cancel   //  已撤销
    }

    public enum NODE_ACTION_STATUS
    {
        Assigned,  //已派发
        Committed,  //已提交
        Approved,  //容许
        Cancelled,   //已作废
        Reject,   //主动驳回
        Deny      //被动驳回   
    }

    public enum CODE_CHECK_NODE
    {
        NONE,
        CC_WAIT,  //开发中
        CC_COMMIT, //提交代码
        CC_R_CHECK, //规范检查
        CC_F_CHECK, //功能检查
        CC_MERGE  //上传合并
    }

    public class Node : ICloneable
    {
        public NODE_ACTION_STATUS CurrentStatus { get; set; }
        public CODE_CHECK_NODE CurrentNode { get; set; }
        public DateTime CreateDate { get; set; }

        public object Clone()
        {
            Node newsheet = new Node();
            newsheet.CurrentNode = this.CurrentNode;
            newsheet.CurrentStatus = this.CurrentStatus;
            newsheet.CreateDate = this.CreateDate;
            return newsheet;
        }

        public override string ToString()
        {
            return string.Format("==> CurrentNode:{0}, WorksheetStatus:{1}, Create:{2}", CurrentNode, CurrentStatus, CreateDate.ToString("hh:MM:ss"));
        }
    }

    public class TransitionState
    {
        public string CurrentState { get; set; }
        public string NextState { get; set; }
    }
}
