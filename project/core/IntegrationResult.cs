using System;
using System.Collections;
using System.Text;
using System.Xml.Serialization;
using ThoughtWorks.CruiseControl.Core.Tasks;
using ThoughtWorks.CruiseControl.Core.Util;
using ThoughtWorks.CruiseControl.Remote;

namespace ThoughtWorks.CruiseControl.Core
{
    /// <summary>
    /// Contains all the results of a project's integration.
    /// </summary>
    [Serializable]
    public class IntegrationResult : IIntegrationResult
    {
		private string _project;
		private IntegrationStatus _lastIntegrationStatus = IntegrationStatus.Unknown;
		private BuildCondition _buildCondition;
		private string _workingDirectory;
		private string _label;
		private IntegrationStatus _status = IntegrationStatus.Unknown;
		private DateTime _startTime;
		private DateTime _endTime;
		private Modification[] _modifications = new Modification[0];
		private string _artifactDirectory;
		private string _projectUrl;
        private Exception _exception;
        private ArrayList _taskResults = new ArrayList();

        // Default constructor required for serialization
        public IntegrationResult()
        {
        }

        public IntegrationResult(string projectName, string workingDirectory)
        {
            ProjectName = projectName;
			_workingDirectory = workingDirectory;
        }

    	[XmlIgnore]
        public virtual Modification[] Modifications
        {
            get { return _modifications; }
            set { _modifications = value; }
        }

        public DateTime LastModificationDate
        {
            get
            {
                DateTime latestDate = DateTime.MinValue;
                if (Modifications.Length == 0)			//TODO: why set the date to yesterday's date as a default
                {										//If there are no modifications then this should be set to the last modification date
                    latestDate = DateTime.Now;			// from the last integration (or 1/1/1980 if there is no previous integration).
                    latestDate = latestDate.AddDays(-1.0);
                }

                foreach (Modification modification in Modifications)
                {
                    latestDate = DateUtil.MaxDate(modification.ModifiedTime, latestDate);
                }
                return latestDate;
            }
        }

		public int LastChangeNumber
		{
			get
			{
				int lastChangeNumber = 0;
				foreach (Modification _modification in _modifications)
				{
					if (_modification.ChangeNumber > lastChangeNumber)
						lastChangeNumber = _modification.ChangeNumber;
				}
				return lastChangeNumber;
			}
		}

        /// <summary>
        /// Gets a value indicating the success of this integration.
        /// </summary>
        public bool Succeeded
        {
            get { return Status == IntegrationStatus.Success; }
        }

        /// <summary>
        /// Gets a value indicating whether this integration failed.
        /// </summary>
        public bool Failed
        {
            get { return Status == IntegrationStatus.Failure; }
        }

        /// <summary>
        /// Gets a value indicating whether this integration fixed a previously broken build.
        /// </summary>
        public bool Fixed
        {
            get { return Succeeded && LastIntegrationStatus == IntegrationStatus.Failure; }
        }

        /// <summary>
        /// Gets a value indicating whether this integration is either successful or in an unknown state.
        /// </summary>
        public bool Working
        {
            get { return Status == IntegrationStatus.Unknown || Succeeded; }
        }

        /// <summary>
        /// Gets the time taken to perform the project's integration.
        /// </summary>
        public TimeSpan TotalIntegrationTime
        {
            get { return EndTime - StartTime; }
        }

        [XmlIgnore] // Exceptions cannot be serialised because of permission attributes
        public Exception ExceptionResult
        {
            get { return _exception; }
            set
            {
                _exception = value;
                if (_exception != null)
                {
                    _status = IntegrationStatus.Exception;
                }
            }
        }

		[XmlIgnore]
		public IList TaskResults
		{
			get { return _taskResults; }
		}

		public string ArtifactDirectory
		{
			get { return _artifactDirectory; }
			set	{ _artifactDirectory = value;}
		}

		public string ProjectUrl
		{
			get { return _projectUrl; }
			set	{ _projectUrl = value; }
		}

    	public void AddTaskResult(string result)
    	{
    		AddTaskResult(new DataTaskResult(result));
    	}

    	public void AddTaskResult(ITaskResult result)
    	{
    		_taskResults.Add(result);
    	}

    	public void MarkStartTime()
        {
            _startTime = DateTime.Now;
        }

        public void MarkEndTime()
        {
            EndTime = DateTime.Now;
        }

    	public bool HasModifications()
        {
            return Modifications.Length > 0;
        }

        public override bool Equals(object obj)
        {
            IntegrationResult other = obj as IntegrationResult;
            if (other == null)
            {
                return false;
            }
            return this.ProjectName == other.ProjectName && this.Status == other.Status && this.Label == other.Label && this.StartTime == other.StartTime && this.EndTime == other.EndTime;
        }

        public override int GetHashCode()
        {
            return (ProjectName + Label + StartTime.Ticks).GetHashCode();
        }

    	public static IntegrationResult CreateInitialIntegrationResult(string project, string workingDirectory)
    	{
    		IntegrationResult result = new IntegrationResult(project, workingDirectory);
    		result.StartTime = DateTime.Now.AddDays(-1);
    		result.EndTime = DateTime.Now;
    		return result;
    	}

    	public IntegrationStatus Status
    	{
    		get { return _status; }
			set { _status = value; }
		}

    	public IntegrationStatus LastIntegrationStatus
    	{
    		get { return _lastIntegrationStatus; }
			set { _lastIntegrationStatus = value; }
    	}

    	public bool IsInitial()
    	{
    		return (LastIntegrationStatus == IntegrationStatus.Unknown) && (Status == IntegrationStatus.Unknown);
    	}

    	public string ProjectName
    	{
    		get { return _project; }
			set { _project = value; }
    	}

    	public BuildCondition BuildCondition
    	{
    		get { return _buildCondition; }
			set { _buildCondition = value; }
    	}

    	public string Label
    	{
    		get { return _label; }
			set { _label = value; }
		}

    	/// <summary>
		/// Gets and sets the date and time at which the integration commenced.
		/// </summary>
		public DateTime StartTime
    	{
    		get { return _startTime; }
			set { _startTime = value; }
		}

		/// <summary>
		/// Gets and sets the date and time at which the integration was completed.
		/// </summary>
		public DateTime EndTime
    	{
    		get { return _endTime; }
			set { _endTime = value; }
    	}

    	public string WorkingDirectory
    	{
    		get { return _workingDirectory; }
			set { _workingDirectory = value; }
		}

		/// <summary>
		/// Determines whether a build should run.  A build should run if there
		/// are modifications, and none have occurred within the modification
		/// delay.
		/// </summary>
		public bool ShouldRunBuild(int modificationDelaySeconds)
		{
			if (Remote.BuildCondition.ForceBuild == BuildCondition) 
				return true;
			else return (HasModifications() &&
					 ! DoModificationsExistWithinModificationDelay(modificationDelaySeconds));
		}

		/// <summary>
		/// Checks whether modifications occurred within the modification delay.  If the
		/// modification delay is not set (has a value of zero or less), this method
		/// will always return false.
		/// </summary>
		private bool DoModificationsExistWithinModificationDelay(int modificationDelaySeconds)
		{
			if (modificationDelaySeconds <= 0)
				return false;

			//TODO: can the last mod date (which is the time on the SCM) be compared with now (which is the time on the build machine)?
			TimeSpan diff = DateTime.Now - LastModificationDate;
			if (diff.TotalSeconds < modificationDelaySeconds)
			{
				Log.Info("Changes found within the modification delay");
				return true;
			}

			return false;
		}

		/// <summary>
		/// Contains the output from the build process.  In the case of NAntBuilder, this is the 
		/// redirected StdOut of the nant.exe process.
		/// </summary>
		[XmlIgnore] 
		public string TaskOutput
    	{
    		get {
    			StringBuilder builder = new StringBuilder();
				foreach (ITaskResult result in _taskResults)
				{
					builder.Append(result.Data);
				}
				return builder.ToString();
    		}
    	}
    }
}