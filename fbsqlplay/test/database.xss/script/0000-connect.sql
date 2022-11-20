-- <step name="connect" executable="true" />

CONNECT 'Provider=SQLOLEDB.1;Integrated Security=SSPI;Persist Security Info=False;Initial Catalog=master;Data Source=_server__instance_;Password=$signCertPw$';

GO

SELECT *
  FROM dbo.t_p