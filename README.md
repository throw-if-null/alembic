# Alembic:alembic:
[![Actions Status](https://github.com/MirzaMerdovic/alembic/workflows/CI/badge.svg)](https://github.com/MirzaMerdovic/alembic/actions)

# What is it?
Alembic :alembic: is a simple .net core (3.1) app that monitors the health status of your Docker containers.

# Motivation
I needed a small utility app that I could use for local development and potentially for small systems where I would like to have as few 3rd party tools involved in my 
infrastructure setup as possible.
So, if you are using _Docker Swarm_, _Kubernetes_ or similar you are not going to need this, but if you are working with only _Docker Compose_ or just _Docker_ as far as it is 
now there is no out of the box way to have your containers restarted or killed in case they become unhealthy which is not really an optimal solution.

:alembic: tries to solve this problem by running a simple application written in _C#_ running on _.Net Core_. The application is listening for containers' _"health_status"_ 
events and act if the status is _unhealthy_.

You should also be aware of [docker-autoheal](https://github.com/willfarrell/docker-autoheal) project which is tackling the same issue.

# How it works
_Docker_ is transmitting events about almost everything that's going on with your containers, :alembic: is interested in _"health_status"_ container events and once it receives 
one it will check if the received status is _"healthy"_ or _"unhealthy"_ and act accordingly.

By default :alembic: is going to try and restart container _3_ times and upon _3_ consecutive tries if the container is still reported as _unhealthy_ :alembic: is going to kill 
the container.
Both actions: _restart_ and _kill_ are going to be reported via webhook. For starters only _Slack_ webhooks will be supported.

# How to use :alembic:
Add this configuration in your _docker-compose_ file:

```yml
alembic:
    image: mirzamerdovic/alembic
    volumes:
      - /var/run/docker.sock:/var/run/docker.sock
```

If you want to change the default values you can bind your own appsettings.json (_you can name what ever you want_) with override values
```yml
volumes:
  - ${path_to_appsettings_override}:/app/appsettings.json:ro
```

If you only need to update the webhook URL then you can just add environment variable: `WebHookReporterOptions__Url=${my_url}` to your docker-compose
```yml
environment:
  - WebHookReporterOptions__Url=services/{my_webhook}
```

## Configuration
There are a couple of things that can be configured when using :alembic:
* RestartCount: Tells :alembic: how many time it will try to restart an unhealthy container consecutively
* Kill: Tells :alembic: whether it should kill containers that cannot auto-heal. In other words tells :alembic: what to do once the maximum number of retries have been reached. 
With _true_ meaning kill the container and with _false_ meaning leave the container as it is.
* ReportsWebHook: A webhook to where :alembic: will send the notifications about restart and/or kill actions

## Known Issues
The `appsettings.json` hot reaload is not working.
Which means if you change a value in your bound override of `appesttings.json` it will not be applied until you restart the container. 
The hot reload works if you are running from VS or just executing the dll, but when it is in container nothing is happening.
I saw there are issues with binding to a file and hot-reload, and there was a suggestion to use folder bind, I tried that too, 
with the same result.
Maybe I will get back to this, or maybe someone else will figure out what's the problem.
