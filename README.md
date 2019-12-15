# Alembic:alembic:
Alembic :alembic: is a simple .net core app that monitors the health status of your Docker containers.

# Motivation

I needed a small utility app that I could use for local development and potentially for small system where I would like to have as few 3rd party tools involved in my 
infrastructure setup as possible.
So, if you are using _Docker Swarm_, _Kubernetes_ or similar you are not going to need this, but if you are working with only _Docker Compose_ or just _Docker_ as far as it is 
now there is no out of the box way to have your containers restarted or killed in case they become unhealthy which is not really optimal solution.

:alembic: tries to solve this problem by running a simple application written in _C#_ running on _.Net Core_. The application is listening for containers' _"health_status"_ 
events and act if the status is _unhealthy_.

You should also be aware of [docker-autoheal](https://github.com/willfarrell/docker-autoheal) project which is tackling the same issue.

# How it works

_Docker_ is transmitting events about almost everything that's going on with your containers, :alembic: is interested in _"health_status"_ container events and once it receives 
one it will check if the received status is _"healthy"_ or _"unhealthy"_ and act accordingly.

By default :alembic: is going to try and restart container _3_ times and upon _3_ consecutive tries if the container is still reported as _unhealthy_ :alembic: is going to kill 
the container.
Both actions: _restart_ and _kill_ are going to be report to a webhook or configured _Slack_ channel.

:alembic: can be configured to persist the data to _Mongo_ database so you can review the containers' histories if that is something that you need, otherwise it will operate 
in memory.

# How to use :alembic:

Add this configuration in your _docker-compose_ file:

```yml
alembic:
    image: mirzamerdovic/alembic
    environment:
      -- RestartCoun=3
      -- Kill=true
      -- ConnectionString=mongodb://localhost
      -- ReportWebHook=http://mywebhook.com
    volumes:
      - /var/run/docker.sock:/var/run/docker.sock
```

## Configuration
There are a couple of things that can be configured when using :alembic:
* RestartCount: Tells :alembic: how many time it will try to restart an unhealthy container consecutively
* Kill: Tells :alembic: whether it should kill containers that cannot auto-heal. In other words tells :alembic: what to do once the maximum number of retries has been reached. 
With _true_ meaning kill the container and with _false_ meaning leave the container as it is.
* ConnectionString: Mongodb connection string, if not provided :alembic: will save data in memory
* ReportsWebHook: A webhook to where :alembic: will send the notifications about restart and/or kill actions

# TODOs
## Make retrieving container/service history possible
Expose endpoints so it is possible to get the information about a container or service. For example:
```
curl http://alembic.com/report/my-service
```
Would return the health history of the service and then you can see if the service was restarted or not.