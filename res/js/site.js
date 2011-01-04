$(document).ready(function() {
  $("code").addClass("prettyprint");
  prettyPrint();
  
	var emails = $("span.email");
	
	for (var i = 0; i < emails.length; i++) {
		var p = emails[i].id.split(' ');
		var a = p[0] + '@' + p[1] + '.' + p[2];
		$(emails[i]).append("<a href='mailto:" + a + "'>" + a + "</a>");
	}
});