var express = require('express'),
    sql = require('mssql'),
    cookieParser = require('cookie-parser'),
    bodyParser = require('body-parser'),
    methodOverride = require('method-override'),
    app = express(),
    server = require('http').Server(app),
    io = require('socket.io')(server);

io.set('transports', ['polling']);

var port = process.env.PORT || 4000;
var config = {};

config['server'] = process.env.SQL_HOSTNAME;
config['user'] = process.env.SQL_USERNAME;
config['password'] = process.env.SQL_PASSWORD;
config['database'] = 'VOTEDB';

console.log(config);

io.sockets.on('connection', function (socket) {
    socket.emit('message', { text : 'Welcome!' });
    socket.on('subscribe', function (data) {
	socket.join(data.channel);
    });
});

function getVotes() {
    console.log('Getting votes');
    sql.connect(config).then(pool => {
	return pool.request()
	    .query('SELECT vote, count FROM voteCount')
    }).then(result => {
	var votes = {};
	for (var record of result.recordset) {
	    var vote = record['vote'];
	    var count = record['count'];
	    votes[vote] = count;
	}
	io.sockets.emit("scores", JSON.stringify(votes));

    }).catch(err => {
	console.log('Exception: in catch');
    });
    setTimeout(function() { getVotes() }, 1000);
}			 
 
getVotes();
app.use(cookieParser());
app.use(bodyParser());
app.use(methodOverride('X-HTTP-Method-Override'));
app.use(function(req, res, next) {
    res.header("Access-Control-Allow-Origin", "*");
    res.header("Access-Control-Allow-Headers", "Origin, X-Requested-With, Content-Type, Accept");
    res.header("Access-Control-Allow-Methods", "PUT, GET, POST, DELETE, OPTIONS");
    next();
});

app.use(express.static(__dirname + '/views'));

app.get('/', function (req, res) {
    res.sendFile(path.resolve(__dirname + '/views/index.html'));
});

server.listen(port, function () {
    var port = server.address().port;
    console.log('App running on port ' + port);
});
