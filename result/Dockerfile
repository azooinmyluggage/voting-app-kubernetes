FROM node:10.15.2-slim

WORKDIR /app

RUN npm install -g nodemon
ADD . /app
RUN npm install 

ENV PORT 8080
ENV SQL_HOSTNAME sql_hostname
ENV SQL_USERNAME sql_username
ENV SQL_PASSWORD sql_password

EXPOSE 8080

CMD ["node", "server.js"]
